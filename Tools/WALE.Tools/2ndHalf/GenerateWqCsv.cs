using System.Collections;
using System.Globalization;
using System.Text;
using CsvHelper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.Tools._2ndHalf.Configuration;
using WALE.Tools._2ndHalf.Models;
using WALE.Tools.Config;

namespace WALE.Tools._2ndHalf;

public static class GenerateWqCsv
{
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    
    public static async Task RunAsync()
    {
        ConsoleHelper.WriteLine("Started generating water quality form csv");
        
        var httpClient = HttpHelper.GetResilientHttpClient(
            KeyConfig.ApiBaseUrl,
            100,
            30);
        
        ICacheService cacheService = new ApiCacheService(httpClient);
        IOutputService outputService = new ApiOutputService(httpClient);
        IMessageQueueService messageQueueService  = new ApiMessageQueueService(httpClient);
        
        var maxConcurrentScrapers = 10;
        var pdfDataExtractors = GetPdfDataExtractors(
            cacheService,
            outputService,
            messageQueueService,
            maxConcurrentScrapers);

        var fileService = new ApiFileService(httpClient);
        var files = (await fileService.GetAllFilesAsync())
            .Where(f => f.StartsWith("wq__", StringComparison.InvariantCultureIgnoreCase))
            .ToList();
        
        const int processRunId = -98;
        
        var lookupConfiguration = LookupConfiguration(fileService, cacheService, outputService);
        var uniqueFolder = $"WQ-{DateTime.Today:yyyyMMdd}";
        
        await using var writer = new StreamWriter(
            $"{uniqueFolder}.csv",
            false,
            Encoding.Unicode);
        
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));
        var lines = new List<WqFormCsvLine>();
        
        var scrapingTasks = new List<Task<WqFormCsvLine?>>();
        var processCount = 1;

        /*files = files
            .Where(f => f.StartsWith("wq__002728-fn-01__7c129eb4-4149-23a9-130f-58b32d03aa50.pdf",
                StringComparison.OrdinalIgnoreCase))
            .ToList();*/
        
        foreach (var filepath in files)
        {
            var pdfDataExtractor = pdfDataExtractors.First(extractor => !extractor.InUse);
            pdfDataExtractor.InUse = true;
            
            scrapingTasks.Add(
                ScrapeDocumentAsync(
                    filepath,
                    lookupConfiguration,
                    pdfDataExtractor,
                    processRunId,
                    processCount++,
                    files.Count));

            while (scrapingTasks.Count >= maxConcurrentScrapers)
            {
                await Task.WhenAny(scrapingTasks);
                var toRemoveList = new List<Task<WqFormCsvLine?>>();

                foreach (var scrapingTask in scrapingTasks)
                {
                    if (!scrapingTask.IsCompleted)
                    {
                        continue;
                    }

                    var result = scrapingTask.Result;

                    if (result != null)
                    {
                        lines.Add(result);
                    }

                    toRemoveList.Add(scrapingTask);
                }

                foreach (var toRemoveItem in toRemoveList)
                {
                    scrapingTasks.Remove(toRemoveItem);
                }
            }
        }
        
        foreach (var scrapingTask in scrapingTasks)
        {
            var result = scrapingTask.Result;

            if (result != null)
            {
                lines.Add(result);
            }
        }
        
        foreach (var pdfDataExtractor in pdfDataExtractors)
        {
            pdfDataExtractor.Dispose();   
        }
        
        await csv.WriteRecordsAsync((IEnumerable)lines);
        ConsoleHelper.WriteLine("Finished generating water quality form csv");
    }
    
    private static List<IPdfDataExtractorService> GetPdfDataExtractors(
        ICacheService cacheService,
        IOutputService outputService,
        IMessageQueueService messageQueueService,
        int maxConcurrentScrapers)
    {
        var pdfDataExtractors = new List<IPdfDataExtractorService>();

        for (var idx = 0; idx < maxConcurrentScrapers; idx++)
        {
            var id = idx + 1;

            pdfDataExtractors.Add(new PdfDataExtractorService(
                new PdfPigNoOcrDataExtractorService(),
                new List<IOcrDataExtractorService>(),
                cacheService,
                outputService,
                DocumentService,
                DocnetAlternativeDocumentService,
                messageQueueService,
                id: id));
        }

        return pdfDataExtractors;
    }

    private static async Task<WqFormCsvLine?> ScrapeDocumentAsync(
        string filepath,
        LookupConfiguration lookupConfiguration,
        IPdfDataExtractorService pdfDataExtractor,
        int processRunId,
        int fileNumber,
        int totalNumber)
    {
        try
        {
            var fileName = Path.GetFileName(filepath);

            var dtStart = DateTime.Now;
            ConsoleHelper.WriteLine(
                $"INFO - {nameof(GenerateWqCsv)}:{pdfDataExtractor.Id} - Started {fileName} ({fileNumber} of {totalNumber}) at {dtStart:yyyy-MM-dd HH:mm:ss}");

            var fileId = FileHelper.ExtractFileId(fileName);
            if (fileId == null)
            {
                ConsoleHelper.WriteLine($"ERROR - {fileName} doesn't contain a fileid guid");
                return null;
            }

            var (_, alreadySaved, internalResults) = await GetMatchesAsync(
                fileName,
                fileId.Value,
                lookupConfiguration,
                pdfDataExtractor,
                processRunId);

            if (internalResults == null)
            {
                return null;
            }

            if (alreadySaved == false)
            {
                await pdfDataExtractor.SaveMatchResultAsync(
                    internalResults,
                    fileId.Value,
                    processRunId,
                    lookupConfiguration.UseLockExclusivity);
            }

            var line = internalResults.Matches?
                .FirstOrDefault(match => match.LabelGroupName == "SpecialTermsSingleLine");
            
            var wholeBlock = internalResults.Matches?
                .FirstOrDefault(match => match.LabelGroupName == "SpecialTermsWholeBlock");
            
            return new WqFormCsvLine
            {
                ContainsAnyOfTheLines = line != null,
                FileName = fileName,
                LineText = line != null ? string.Join('\n', line.Text!.Select(t => t.Text)) : null,
                WholeSectionText = wholeBlock != null ? string.Join('\n', wholeBlock.Text!.Select(t => t.Text)) : null,
            };
        }
        finally
        {
            pdfDataExtractor.InUse = false;
        }
    }
    
    private static async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item)> GetMatchesAsync(
        string fileName,
        Guid fileId,
        LookupConfiguration lookupConfiguration,
        IPdfDataExtractorService pdfDataExtractor,
        int processRunId)
    {
        try
        {
            var result = await pdfDataExtractor.GetMatchesAsync(
                fileName,
                new DmsFileData { FileId = fileId },
                lookupConfiguration,
                [fileName],
                processRunId);
            
            return result;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"ERROR - {nameof(GenerateWqCsv)} - {fileName} {ex}");
            return (true, (bool?)null, (MatchesResult?)null);
        }
    }
    
    private static LookupConfiguration LookupConfiguration(
        IFileService fileService,
        ICacheService cacheService,
        IOutputService outputService)
    {
        return new LookupConfiguration(
            WqFormLabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            null!,
            null!,
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            skipFileIfMoreThenPages: 100,
            useLockExclusivity: false);
    }
}