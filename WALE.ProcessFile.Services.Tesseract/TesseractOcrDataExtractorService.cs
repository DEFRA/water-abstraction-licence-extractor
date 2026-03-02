using System.Diagnostics;
using System.Text.Json;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

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
    public string Name => $"TesseractOcr-{pageSegMode}";
    public int Id { get; set; } = id;
    
    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(
            string imageReference,
            string pdfFilepath,
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
            Filepath = pdfFilepath,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };

        var isPageScreenshot = OcrHelper.IsPageScreenshot(imageReference, pageNumber);
        var returnLines = new List<LineAndWords>();

        var cachedJson = isPageScreenshot
            ? await cacheService.GetOcrScreenshotTextAsync(request)
            : await cacheService.GetOcrImageTextAsync(request);
        
        if (pdfDocument.FromCache && !string.IsNullOrEmpty(cachedJson))
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
                    pdfFilepath,
                    processRunId);
            }
            else
            {
                var externalProcessRanOk = await RunSeparateTesseractProcessAsync(
                    pageNumber,
                    imageNumber,
                    imageReference,
                    pdfFilepath,
                    isPageScreenshot,
                    processRunId,
                    cacheService.UsesDatabase);

                if (externalProcessRanOk == ProcessResult.UnknownOrTransientError)
                {
                    // TODO - Log
                    ConsoleHelper.WriteLine($"ERROR - {Name} - Transient error occured (see above)");

                    // Don't cache, should work next time
                    canSave = false;
                }
                else if (externalProcessRanOk == ProcessResult.RepeatableError)
                {
                    // Never going to get a result back

                    await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);
                    await cacheService.SaveOcrImageTextAsync(request, returnLines);
                }
                else
                {
                    if (isPageScreenshot)
                    {
                        returnLines = await cacheService.GetTemporaryOcrScreenshotTextAsync(request);
                    }
                    else
                    {
                        returnLines = await cacheService.GetTemporaryOcrImageTextAsync(request);
                    }
                }
            }

            if (canSave)
            {
                if (isPageScreenshot)
                {
                    await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);
                    //await cacheService.DeleteTemporaryOcrScreenshotTextAsync(request);                    
                }
                else
                {
                    await cacheService.SaveOcrImageTextAsync(request, returnLines);
                    //await cacheService.DeleteOcrImageTextAsync(request, returnLines);
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
            maxPositiveDiffBetweenWordTop,
            multiplyConfidenceBy: 1);
    }
    
    private async Task<ProcessResult> RunSeparateTesseractProcessAsync(
        int pageNumber,
        int imageNumber,
        string imageReference,
        string pdfFilePath,
        bool isPageScreenshot,
        int processRunId,
        bool isDbBased)
    {
        var pdfFileNameOnly = Path.GetFileNameWithoutExtension(pdfFilePath);
        var showDebugMessages = false;

        if (!showDebugMessages)
        {
            ConsoleHelper.WriteLine($"INFO - {Name} (P{pageNumber}, I{imageNumber}, {pdfFileNameOnly}) - External process called");
        }
        
        var fileMode = isDbBased ? "Database" : "File";

        if (string.IsNullOrWhiteSpace(cacheService.CacheFolderOrUrl))
        {
            ConsoleHelper.WriteLine("ERROR - TesseractOcr - Tesseract Exe cannot be used when using DB cache locally");
            throw new Exception("Tesseract Exe cannot be used when using DB cache locally");
        }
        
        var argumentsList = string.Join(" ", new List<string>
        {
            tesseractExeName,
            pageSegMode.ToString(),
            fileMode,
            pageNumber.ToString(),
            imageNumber.ToString(),
            $"\"{imageReference}\"",
            $"\"{pdfFilePath}\"",
            isPageScreenshot.ToString(),
            processRunId.ToString(),
            $"\"{cacheService.CacheFolderOrUrl}\"",
            $"\"{outputService.OutputFolder}\"",
            $"\"{tessDataPath}\""
        });
        
        var proc = Process.Start(
            new ProcessStartInfo
            {
                WorkingDirectory = tesseractExeDirectory,
                Arguments = argumentsList,
                FileName = dotnetPath,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            });
        
        await proc!.WaitForExitAsync();

        while (!proc.StandardError.EndOfStream)
        {
            var line = await proc.StandardError.ReadLineAsync();
            const string errorPrefix = "\"Error: ";
            
            if (line?.StartsWith(errorPrefix, StringComparison.Ordinal) == true)
            {
                var repeatableErrors = new List<string>
                {
                    "Assert failed"
                };

                if (repeatableErrors.Any(repeatableError => line.Contains(repeatableError, StringComparison.Ordinal)))
                {
                    ConsoleHelper.WriteLine($"WARNING - {Name} - Failed with error: {line}");
                    return ProcessResult.RepeatableError;
                }
                
                proc.Kill();

                var exceptionMessage = line[line.IndexOf(errorPrefix, StringComparison.Ordinal)..];
                ConsoleHelper.WriteLine($"ERROR - {Name} - External Tesseract process gave error: {exceptionMessage}");
                
                return ProcessResult.UnknownOrTransientError;
            }

            if (showDebugMessages)
            {
                ConsoleHelper.WriteLine($"DEBUG - {Name} (P{pageNumber}, I{imageNumber}, {pdfFileNameOnly}) - {line}");
            }
        }
        
        if (proc.ExitCode == 0)
        {
            return ProcessResult.Ok;
        }
        
        ConsoleHelper.WriteLine($"ERROR - External process errored with exit code {proc.ExitCode}");
        // TODO - Log error
        
        return ProcessResult.UnknownOrTransientError;
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}