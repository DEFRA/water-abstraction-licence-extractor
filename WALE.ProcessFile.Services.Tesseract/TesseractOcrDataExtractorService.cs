using System.Diagnostics;
using System.Text.Json;
using NetMQ;
using NetMQ.Sockets;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OcrService;
using WALE.ProcessFile.Services.Cache;

namespace WALE.ProcessFile.Services.Tesseract;

public class TesseractOcrDataExtractorService(
    string tessDataPath,
    PageSegMode pageSegMode,
    ICacheService cacheService,
    IOutputService outputService,
    string dotnetPath,
    string tesseractExeName,
    string tesseractExeDirectory,
    int id = -1)
    : IOcrDataExtractorService, IDisposable
{
    public bool HasDirectCost => false;
    public OcrServiceFailurePolicy FailurePolicy => OcrServiceFailurePolicy.DegradeOnError;
    public string Name => $"TesseractOcr-{pageSegMode}";
    public int Id { get; set; } = id;

    public const string StartedOkay = "STARTED:OK";
    public const string FinishedPrefix = "FINISHED:";
    public const string ErroredPrefix = "ERROR:";
    public const string Acknowledged = "ACKKNOWLEDGED:OK";

    private List<LineAndWords>? _response = null;
    
    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(
            string imageReference,
            int pageNumber,
            int imageNumber,
            PdfDocument pdfDocument,
            int processRunId,
            string noOcrServiceName)
    {
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            FileId = pdfDocument.FileId,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };

        var isPageScreenshot = OcrHelper.IsPageScreenshot(imageReference, pageNumber);
        var returnLines = new List<LineAndWords>();

        var cachedJson = pdfDocument.FromCache
            ? await (isPageScreenshot
                ? cacheService.GetOcrScreenshotTextAsync(request)
                : cacheService.GetOcrImageTextAsync(request))
            : null;
        
        if (false && pdfDocument.FromCache && !string.IsNullOrEmpty(cachedJson))
        {
            var imageLines = JsonSerializer.Deserialize<List<LineAndWords>>(
                cachedJson,
                JsonHelper.GetSerializerOptions());

            returnLines.AddRange(imageLines!);
        }
        else
        {
            var canSave = true;
            
            // NOTE - Following is intended for debugging - shouldn't be set for long, as some files
            // crash Tesseract and take our process down with it
            var runTesseractInsideThisProcess = false; 

            if (runTesseractInsideThisProcess)
            {
                try
                {
                    var dtStart = DateTime.UtcNow;
                    ConsoleHelper.WriteLine($"INFO - {Name} (P{pageNumber}, I{imageNumber}, {pdfDocument.FileId}) - Tesseract in-process called");
                    
                    var inprocessTesseractService = new InternalTesseractOcrDataExtractorService(
                        outputService,
                        cacheService,
                        tessDataPath,
                        pageSegMode);

                    returnLines = await inprocessTesseractService.ProcessAsync(
                        GeneralConstants.PdfPigDataExtractorServiceName,
                        pageNumber,
                        imageNumber,
                        isPageScreenshot,
                        imageReference,
                        pdfDocument.FileId,
                        processRunId);
                    
                    var tsDuration = (DateTime.UtcNow - dtStart).TotalSeconds.ToString("0.0");
                    ConsoleHelper.WriteLine($"INFO - {Name} - (P{pageNumber}, I{imageNumber}, {pdfDocument.FileId}) - Received response in {tsDuration} seconds");
                }
                catch (Exception ex)
                {
                    ConsoleHelper.WriteLine($"ERROR - {Name} - Error occurred processing {imageReference} - {ex}");

                    throw new OcrServiceTransientException(
                        Name,
                        $"{Name} failed for P{pageNumber}, I{imageNumber}, {pdfDocument.FileId}: " +
                        $"{ex.GetType().Name} - {ex.Message}",
                        ex);
                }
            }
            else
            {
                var externalProcessRanOk = await RunSeparateTesseractProcessAsync(
                    pageNumber,
                    imageNumber,
                    imageReference,
                    pdfDocument.FileId,
                    isPageScreenshot,
                    processRunId);
                
                if (externalProcessRanOk == ProcessResult.UnknownOrTransientError)
                {
                    // Thrown rather than returned because only PdfDataExtractorService's catch
                    // populates ServicesFailed. The exit code cannot tell a missing image from a
                    // timeout, so it is reported as transient; FailurePolicy is DegradeOnError,
                    // so the file is not failed either way.
                    throw new OcrServiceTransientException(
                        Name,
                        $"{Name} external process failed for P{pageNumber}, I{imageNumber}, " +
                        $"{pdfDocument.FileId} - {imageReference}",
                        new Exception("Tesseract external process returned a non-zero exit code"));
                }
                
                if (externalProcessRanOk == ProcessResult.RepeatableError)
                {
                    // Never going to get a result back

                    await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);
                    await cacheService.SaveOcrImageTextAsync(request, returnLines);
                }
                else
                {
                    returnLines = _response;
                    canSave = false;
                }
            }

            if (canSave && returnLines != null)
            {
                if (isPageScreenshot)
                {
                    await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);              
                }
                else
                {
                    await cacheService.SaveOcrImageTextAsync(request, returnLines);
                }
            }
        }

        const int horizontalColumnGap = 200;
        const int minFontSize = 15;
        const int maxPercentHeightDiff = 0;

        const int lineHeight = 21;
        const int maxNegativeDiffBetweenWordTop = -100;
        const int maxPositiveDiffBetweenWordTop = 100;
        const int considerableOverlapAmount = 3; // TODO check and tweak

        return await OcrHelper.GroupAsync(
            returnLines,
            false,
            pageNumber,
            horizontalColumnGap,
            minFontSize,
            considerableOverlapAmount,
            lineHeight,
            maxPercentHeightDiff,
            maxNegativeDiffBetweenWordTop,
            maxPositiveDiffBetweenWordTop);
    }
    
    private async Task<ProcessResult> RunSeparateTesseractProcessAsync(
        int pageNumber,
        int imageNumber,
        string imageReference,
        Guid fileId,
        bool isPageScreenshot,
        int processRunId)
    {
        try
        {
            var showDebugMessages = false;

            if (!showDebugMessages)
            {
                ConsoleHelper.WriteLine(
                    $"INFO - {Name} (P{pageNumber}, I{imageNumber}, {fileId}) - External process called");
            }

            var fileMode = cacheService is ApiCacheService or DatabaseCacheService ? "Database" : "File";

            if (string.IsNullOrWhiteSpace(cacheService.CacheFolderOrUrl))
            {
                ConsoleHelper.WriteLine($"ERROR - {Name} - Tesseract Exe cannot be used when using DB cache locally");
                throw new Exception("Tesseract Exe cannot be used when using DB cache locally");
            }

            const int listenPortNumber = 5556;

            var argumentsList = string.Join(" ", new List<string>
            {
                tesseractExeName,
                pageSegMode.ToString(),
                fileMode,
                pageNumber.ToString(),
                imageNumber.ToString(),
                $"\"{imageReference}\"",
                $"\"{fileId}\"",
                isPageScreenshot.ToString(),
                processRunId.ToString(),
                $"\"{cacheService.CacheFolderOrUrl}\"",
                $"\"{outputService.OutputFolder}\"",
                $"\"{tessDataPath}\"",
                listenPortNumber.ToString()
            });

            using var runtime = new NetMQRuntime();
            runtime.Run(CommunicateAsync(
                listenPortNumber,
                argumentsList,
                imageReference,
                showDebugMessages,
                pageNumber,
                imageNumber,
                fileId));
        }
        catch (Exception ex)
        {
            // TODO
        }
        
        return ProcessResult.Ok;
    }

    private async Task CommunicateAsync(
        int listenPortNumber,
        string argumentsList,
        string imageReference,
        bool showDebugMessages,
        int pageNumber,
        int imageNumber,
        Guid fileId)
    {
        using var server = new ResponseSocket($"@tcp://localhost:{listenPortNumber}");
            
        var proc = Process.Start(
            new ProcessStartInfo
            {
                WorkingDirectory = tesseractExeDirectory,
                Arguments = argumentsList,
                FileName = dotnetPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            });

        var timedOut = false;
        const int timeoutMsFull = 35_000;
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(TimeSpan.FromMilliseconds(timeoutMsFull));
        
        var cancellationToken = cts.Token;
        cancellationToken.Register(() =>
        {
            timedOut = true;
            
            try
            {
                proc!.Kill();
            }
            catch
            {
                // ignored
            }
        });
        
        if (timedOut)
        {
            ConsoleHelper.WriteLine($"ERROR - {Name} - External Tesseract process timed out- {imageReference}");
        }

        var externalResponseTask = server.ReceiveFrameStringAsync(cancellationToken);
        var exitTask = proc!.WaitForExitAsync(cancellationToken);
        
        var firstAction = await Task.WhenAny(externalResponseTask, exitTask);
        if (firstAction == exitTask)
        {
            var serverResponseMessage = "[NO MESSAGE RETURNED]";
            
            if (server.HasIn)
            {
                serverResponseMessage = externalResponseTask.IsCompleted
                    ? (await externalResponseTask).Item1
                    : server.ReceiveFrameString();
            }
            
            ConsoleHelper.WriteLine($"ERROR - {Name} - External process closed unexpectedly before handshake " +
                $"with exit code {proc.ExitCode} / {serverResponseMessage} - {imageReference}");
            // TODO - Log error

            return;
        }

        var handshakeResponse = (await externalResponseTask).Item1;

        if (handshakeResponse == StartedOkay)
        {
            ConsoleHelper.WriteLine($"INFO - {Name} - External process started okay");
        }
        else
        {
            ConsoleHelper.WriteLine($"INFO - {Name} - External process returned {handshakeResponse}");
            return;
        }
        
        server.SendFrame(Acknowledged);
        
        externalResponseTask = server.ReceiveFrameStringAsync(cancellationToken);
        var finishAction = await Task.WhenAny(externalResponseTask, exitTask);
        
        if (finishAction == exitTask)
        {
            var serverResponseMessage = "[NO MESSAGE RETURNED]";
            
            if (server.HasIn)
            {
                serverResponseMessage = externalResponseTask.IsCompleted
                    ? (await externalResponseTask).Item1
                    : server.ReceiveFrameString();
            }
            
            ConsoleHelper.WriteLine($"ERROR - {Name} - External process closed unexpectedly " +
                $"with exit code {proc.ExitCode} / {serverResponseMessage} - {imageReference}");
            // TODO - Log error

            return;
        }
        
        var responseString = (await externalResponseTask).Item1;
        var isFinishedOkay = responseString.StartsWith(FinishedPrefix);

        ConsoleHelper.WriteLine(isFinishedOkay
            ? $"INFO - {Name} - Tesseract external completed successfully for {imageReference}"
            : $"ERROR - {Name} - Unexpectedly received error {responseString} - {imageReference}");

        _response = JsonSerializer.Deserialize<List<LineAndWords>>(responseString[FinishedPrefix.Length..]);
        await exitTask;
        
        /*
        // ReSharper disable once MoveVariableDeclarationInsideLoopCondition
        string? line;

        while ((line = await proc.StandardError.ReadLineAsync(cancellationToken)) is not null)
        {
            const string coreTesseractErrorPrefix = "\"Error: ";

            if (line.StartsWith(coreTesseractErrorPrefix, StringComparison.Ordinal))
            {
                var repeatableErrors = new List<string>
                {
                    "Assert failed"
                };

                if (repeatableErrors.Any(repeatableError => line.Contains(repeatableError, StringComparison.Ordinal)))
                {
                    ConsoleHelper.WriteLine($"WARNING - {Name} - Failed with error: {line}");
                    return;
                }

                proc.Kill();

                var exceptionMessage = line[line.IndexOf(coreTesseractErrorPrefix, StringComparison.Ordinal)..];
                ConsoleHelper.WriteLine($"ERROR - {Name} - External Tesseract process gave error: {exceptionMessage}");

                return;
            }

            if (showDebugMessages)
            {
                ConsoleHelper.WriteLine($"DEBUG - {Name} (P{pageNumber}, I{imageNumber}, {fileId}) - {line}");
            }
        }*/
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}