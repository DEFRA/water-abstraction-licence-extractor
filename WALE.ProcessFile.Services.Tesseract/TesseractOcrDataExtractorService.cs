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

    public const string ExeStartedOkay = "EXE_STARTED:OK";
    public const string TesseractSetupOkay = "TESSERACT_SETUP:OK";
    public const string FinishedPrefix = "FINISHED:";
    public const string ErroredPrefix = "ERROR:";
    public const string SharedArgsPrefix = "SHARED_ARGS:";
    public const string PerFileArgsPrefix = "PER_FILE_ARGS:";
    
    private List<LineAndWords>? _response;
    
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

                returnLines = externalProcessRanOk == ProcessResult.RepeatableError ? [] : _response;
            }

            if (returnLines != null)
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
    
    private Task<ProcessResult> RunSeparateTesseractProcessAsync(
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

            var startupArgs = string.Join(" ", new List<string>
            {
                tesseractExeName,
                listenPortNumber.ToString()
            });

            var sharedExeLifecycleArgs = string.Join(" ", new List<string>
            {
                pageSegMode.ToString(),
                fileMode,
                $"\"{cacheService.CacheFolderOrUrl}\"",
                $"\"{outputService.OutputFolder}\"",
                $"\"{tessDataPath}\""
            });
            
            var perFileArgs = string.Join(" ", new List<string>
            {
                pageNumber.ToString(),
                imageNumber.ToString(),
                imageReference,
                fileId.ToString(),
                isPageScreenshot.ToString(),
                processRunId.ToString(),
            });

            using var messageSyncContext = new NetMQRuntime();
            messageSyncContext.Run(CommunicateAsync(
                listenPortNumber,
                startupArgs,
                sharedExeLifecycleArgs,
                perFileArgs,
                imageReference));
        }
        catch (Exception ex)
        {
            // TODO
        }
        
        return Task.FromResult(ProcessResult.Ok);
    }

    private async Task CommunicateAsync(
        int listenPortNumber,
        string startupArgs,
        string sharedExeLifecycleArgs,
        string perFileArgs,
        string imageReference)
    {
        using var server = new ResponseSocket($"@tcp://localhost:{listenPortNumber}");
            
        var proc = Process.Start(
            new ProcessStartInfo
            {
                WorkingDirectory = tesseractExeDirectory,
                Arguments = startupArgs,
                FileName = dotnetPath,
                UseShellExecute = false,
                CreateNoWindow = true
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

        var startupResponseTask = server.ReceiveFrameStringAsync(cancellationToken);
        var processEndedTask = proc!.WaitForExitAsync(cancellationToken);
        
        var firstToFinish = await Task.WhenAny(startupResponseTask, processEndedTask);
        if (firstToFinish == processEndedTask)
        {
            var serverResponseMessage = "[NO MESSAGE RETURNED]";
            
            if (server.HasIn)
            {
                serverResponseMessage = startupResponseTask.IsCompleted
                    ? (await startupResponseTask).Item1
                    : server.ReceiveFrameString();
            }
            
            ConsoleHelper.WriteLine($"ERROR - {Name} - External process closed unexpectedly during startup " +
                $"with exit code {proc.ExitCode} / {serverResponseMessage} - {imageReference}");
            // TODO - Log error

            return;
        }

        var exeStartupResponse = (await startupResponseTask).Item1;

        if (exeStartupResponse == ExeStartedOkay)
        {
            ConsoleHelper.WriteLine($"INFO - {Name} - External process started okay");
        }
        else
        {
            ConsoleHelper.WriteLine($"INFO - {Name} - External process returned {exeStartupResponse}");
            return;
        }
        
        server.SendFrame($"{SharedArgsPrefix}{sharedExeLifecycleArgs}");
        
        var tesseractSetupOkayTask = server.ReceiveFrameStringAsync(cancellationToken);
        firstToFinish = await Task.WhenAny(tesseractSetupOkayTask, processEndedTask);
        
        if (firstToFinish == processEndedTask)
        {
            var serverResponseMessage = "[NO MESSAGE RETURNED]";
            
            if (server.HasIn)
            {
                serverResponseMessage = tesseractSetupOkayTask.IsCompleted
                    ? (await tesseractSetupOkayTask).Item1
                    : server.ReceiveFrameString();
            }
            
            ConsoleHelper.WriteLine($"ERROR - {Name} - External process closed unexpectedly when setting up Tesseract " +
                $"with exit code {proc.ExitCode} / {serverResponseMessage} - {imageReference}");
            // TODO - Log error

            return;
        }
        
        var tesseractSetupResponse = (await tesseractSetupOkayTask).Item1;
        
        if (tesseractSetupResponse == TesseractSetupOkay)
        {
            ConsoleHelper.WriteLine($"INFO - {Name} - External process started Tesseract okay");
        }
        else
        {
            ConsoleHelper.WriteLine($"INFO - {Name} - External process starting Tesseract failed with {tesseractSetupResponse}");
            return;
        }
        
        server.SendFrame($"{PerFileArgsPrefix}{perFileArgs}");
        
        var perFileResponseTask = server.ReceiveFrameStringAsync(cancellationToken);
        firstToFinish = await Task.WhenAny(perFileResponseTask, processEndedTask);
        
        if (firstToFinish == processEndedTask)
        {
            var serverResponseMessage = "[NO MESSAGE RETURNED]";
            
            if (server.HasIn)
            {
                serverResponseMessage = perFileResponseTask.IsCompleted
                    ? (await perFileResponseTask).Item1
                    : server.ReceiveFrameString();
                
                HandlePerFileResponse(serverResponseMessage, imageReference);
                return;
            }
            
            ConsoleHelper.WriteLine($"ERROR - {Name} - External process closed unexpectedly when processing file " +
                $"with exit code {proc.ExitCode} / {serverResponseMessage} - {imageReference}");
            // TODO - Log error

            return;
        }
        
        var perFileResponse = (await perFileResponseTask).Item1;
        HandlePerFileResponse(perFileResponse, imageReference);

        await processEndedTask;
    }

    private void HandlePerFileResponse(string perFileResponse, string imageReference)
    {
        var isFinishedOkay = perFileResponse.StartsWith(FinishedPrefix);

        ConsoleHelper.WriteLine(isFinishedOkay
            ? $"INFO - {Name} - Tesseract external completed successfully for {imageReference}"
            : $"ERROR - {Name} - Unexpectedly received error {perFileResponse} - {imageReference}");

        _response = JsonSerializer.Deserialize<List<LineAndWords>>(perFileResponse[FinishedPrefix.Length..]);
    }
    
    public void Dispose()
    {
        // TODO release managed resources here
    }
}