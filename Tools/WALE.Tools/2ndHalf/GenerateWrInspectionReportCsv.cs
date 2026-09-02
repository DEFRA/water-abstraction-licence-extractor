using System.Collections;
using System.Globalization;
using System.IO.Compression;
using System.Text;
using CsvHelper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Csv;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WALE.Tools._2ndHalf;

public static class GenerateWrInspectionReportCsv
{
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();

    public static async Task<int> GenerateCsvAsync()
    {
        ConsoleHelper.WriteLine("Started generating wr51s csv");
        
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
        
        const bool useS3Api = true;
        List<string> files;
        IFileService fileService;
        
        if (useS3Api)
        {
            fileService = new ApiFileService(httpClient);
            files = (await fileService.GetAllFilesAsync())
                .Where(f => f.StartsWith("wr51__", StringComparison.InvariantCultureIgnoreCase))
                .ToList();
        }
        else
        {
            var folderPath = "/Users/ryanbarlow/Downloads/WR51s/";
            files = Directory.GetFiles(folderPath).ToList();
            
            fileService = new LocalFileService(folderPath);
        }
        
        const int processRunId = -99;
        
        // Filter out ones we've already done (useful for speed only - results in a file that isn't right)
        /*var existingMatchResultsList = await outputService.GetSimpleMatchResults(processRunId);
        var existingMatchResultsDict = existingMatchResultsList.ToDictionary(
            item => item.Filename!,
            item => item.Status!);
            
        // Part of filtering above (usually should be commented out)
        files = files
            .Where(f => !existingMatchResultsDict.ContainsKey(f))
            .ToList();*/
        
        // Debugging helper lines below
        /*files = files
            .Where(f => f == "wr51__03280030052gr__b8180772-8ba3-d92e-a5c9-d66b2cf9d5ef.pdf")
            .Take(20)
            .ToList();*/
        
        var lookupConfiguration = LookupConfiguration(fileService, cacheService, outputService);
        var uniqueFolder = $"WR51-{DateTime.Today:yyyyMMdd}";
        
        await using var writer = new StreamWriter(
            $"{uniqueFolder}.csv",
            false,
            Encoding.Unicode);
        
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));
        var lines = new List<WrInspectionReportCsvLine>();
        
        var scrapingTasks = new List<Task<WrInspectionReportCsvLine?>>();
        var processCount = 1;
        
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
                var toRemoveList = new List<Task<WrInspectionReportCsvLine?>>();

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
        
        var zipFileName = $"{uniqueFolder}-images.zip";

        if (File.Exists(zipFileName))
        {
            File.Delete(zipFileName);
        }

        await ZipFile.CreateFromDirectoryAsync("Images", zipFileName);
        
        await csv.WriteRecordsAsync((IEnumerable)lines);
        ConsoleHelper.WriteLine("Finished generating wr51s csv");
        
        return 1;
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


    private static async Task<WrInspectionReportCsvLine?> ScrapeDocumentAsync(
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
                $"INFO - {nameof(GenerateWrInspectionReportCsv)}:{pdfDataExtractor.Id} - Started {fileName} ({fileNumber} of {totalNumber}) at {dtStart:yyyy-MM-dd HH:mm:ss}");

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
                    processRunId);
            }
            var dmsFileData = new DmsFileData { FileId = fileId.Value };
            var parsedForm = WrInspectionReportSchemaConverter.ToForm(internalResults, dmsFileData);

            // TODO don't do this now its gone to API based
            if (false && parsedForm.Images.Count > 0)
            {
                var firstImage = parsedForm.Images.First();
                var pathParts = firstImage.Split('/');
                var folderName = pathParts[0];

                Directory.CreateDirectory($"Images/{folderName}");

                foreach (var image in parsedForm.Images)
                {
                    var filenameParts = Path.GetFileNameWithoutExtension(image).Split('-');
                    var serviceName = filenameParts[0];
                    var pageNumber = int.Parse(filenameParts[1].Replace("page", string.Empty));
                    var imageNumber = int.Parse(filenameParts[2].Replace("image", string.Empty));

                    var outputFolder = $"{parsedForm.Metadata.FileId}/{serviceName}/Images";
                    var partialOutputFilename = $"page-{pageNumber}-image-{imageNumber}";

                    var sourceImages = Directory.GetFiles($"Cache/{outputFolder}");
                    var sourceImage = sourceImages
                        .Select(Path.GetFileName)
                        .Single(f => f!.StartsWith(partialOutputFilename, StringComparison.InvariantCultureIgnoreCase));

                    var sourceFullPath = sourceImages.Single(i =>
                        i.Contains(sourceImage!, StringComparison.InvariantCultureIgnoreCase));
                    var destinationFullPath = $"Images/{image}";

                    File.Copy(sourceFullPath, destinationFullPath, true);
                }
            }

            return WrInspectionReportCsvLine.FromForm(parsedForm);
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
            ConsoleHelper.WriteLine($"ERROR - {nameof(GenerateCsvAsync)} - {fileName} {ex}");
            return (true, (bool?)null, (MatchesResult?)null);
        }
    }
    
    private static LookupConfiguration LookupConfiguration(
        IFileService fileService,
        ICacheService cacheService,
        IOutputService outputService)
    {
        return new LookupConfiguration(
            WrInspectionReportLabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            new NullLicenceNumberService(),
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            lineHeight: 6,
            skipFileIfMoreThenPages: 100,
            skipFileIfMoreThenImages: 1000,
            minimumRowsForDigital: 30);
    }
}