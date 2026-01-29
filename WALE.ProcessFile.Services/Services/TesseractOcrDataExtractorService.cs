using System.Diagnostics;
using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Enums;

namespace WALE.ProcessFile.Services.Services;

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
                
                // Don't cache, should work next time
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
                    await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);
                }
                else
                {
                    returnLines = await cacheService.GetTemporaryOcrImageTextAsync(request);
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

        return OcrHelper.Group(
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
        string pdfFilePath,
        bool isPageScreenshot,
        int processRunId,
        bool isDbBased)
    {
        var fileMode = isDbBased ? "Database" : "File";
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
            $"\"{cacheService.CacheFolder}\"",
            $"\"{outputService.OutputFolder}\"",
            $"\"{tessDataPath}\"",
            $"\"{cacheService.Host ?? "N/A"}\"",
            $"\"{cacheService.Port}\"",
            $"\"{cacheService.DatabaseName ?? "N/A"}\"",
            $"\"{cacheService.Username ?? "N/A"}\"",
            $"\"{cacheService.Password ?? "N/A"}\""
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
                    return ProcessResult.RepeatableError;
                }
                
                proc.Kill();

                var exceptionMessage = line[line.IndexOf(errorPrefix, StringComparison.Ordinal)..];
                Console.WriteLine($"ERROR - External process gave error '{exceptionMessage}'");
                
                return ProcessResult.UnknownOrTransientError;
            }
            
            Console.WriteLine(line);
        }
        
        if (proc.ExitCode == 0)
        {
            return ProcessResult.Ok;
        }
        
        Console.WriteLine($"ERROR - External process errored with exit code {proc.ExitCode}");
        // TODO - Log error
        
        return ProcessResult.UnknownOrTransientError;
    }

    public void Dispose()
    {
        // TODO release managed resources here
    }
}