using Google.Protobuf.WellKnownTypes;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Helpers;
using WALE.ProcessFile.RuleEngine.Services;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.Tools.Config;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools._1stHalf;

public static class GenerateLicenceReaderExtract
{
    private static readonly Dictionary<string, DmsFileData> DmsFileData = [];

    // Hard-coded list of files to exclude from processing
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "42901G0004__4-29-01-G-0004 6079081.PDF",
        "42903G0001__4-29-03-G-0001 6079427.PDF",
        "42901G0001__Application Transfer Issued Licence 18.12.24.pdf",
        "42902S0004__4-29-02-S-0004 6079168.PDF",
        "42901S0033R01__Application Transfer Issued Licence 18.12.24.pdf"
    };

    private static PdfDataExtractorService CreatePdfDataExtractorService(
        int id,
        ICacheService cacheService,
        IOutputService outputService,
        INoOcrPdfDocumentService documentService,
        INoOcrAlternativePdfDocumentService alternativeDocumentService)
    {
        var dotnetPath = KeyConfig.DotnetPath;
        var tesseractExeName = KeyConfig.TesseractExeName;
        var tesseractExeDirectory = KeyConfig.TesseractExeDirectory;

        return new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new TesseractOcrDataExtractorService(
                    KeyConfig.TesseractPrefix, 
                    PageSegMode.SparseTextOsd, 
                    cacheService, 
                    outputService,
                    dotnetPath, 
                    tesseractExeName, 
                    tesseractExeDirectory,
                    id),
                new TesseractOcrDataExtractorService(
                    KeyConfig.TesseractPrefix,
                    PageSegMode.Auto, 
                    cacheService, 
                    outputService,
                    dotnetPath, 
                    tesseractExeName, 
                    tesseractExeDirectory, 
                    id),
                new AzureAiVisionOcrDataExtractorService(
                    KeyConfig.AiVisionEndpoint,
                    KeyConfig.AiVisionKey,
                    cacheService,
                    outputService,
                    id)
            },
            cacheService,
            outputService,
            documentService,
            alternativeDocumentService);
    }

    public static async Task<int> GenerateLicenceReaderExtractAsync(bool includeVersionMatch)
    {
        var dtStart = DateTime.Now;
        ConsoleHelper.WriteLine($"API is {KeyConfig.ApiBaseUrl}");
        
        var httpClient = HttpHelper.GetResilientHttpClient(
            KeyConfig.ApiBaseUrl,
            100,
            30);

        var delayPerProcessMs = 500;
        
        //var maxConcurrentScrapers = 6;
        var maxConcurrentScrapers = 10;
        
        var cacheService = new ApiCacheService(httpClient);
        var outputService = new ApiOutputService(httpClient);
        
        var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        
        ConsoleHelper.WriteLine("Started getting nald licence status");
        var naldLicenceStatusDataTask = cacheService.GetNaldLicenceStatusDataAsync();
        
        const int take = 10_000;

        var dmsExtractInfoTask = GetDmsExtractInfoAsync(cacheService, take);
        var allNaldData = await GetAllNaldDataAsync(cacheService, take);
        
        ConsoleHelper.WriteLine("Finished getting all nald data");
        
        LicenceNumber.Instance = new LicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);

        var comparableAbstractionLicences = new Dictionary<string, List<NaldAbstractionLicenceDataLine>>();

        foreach (var naldLine in allNaldData.AbstractionLicences!)
        {
            var comparisonLicenceNumber = FormattingHelper.StripForComparison(
                naldLine.LicenceNo,
                naldLine.FgacRegionCode);

            if (!comparableAbstractionLicences.TryAdd(comparisonLicenceNumber!, [naldLine]))
            {
                comparableAbstractionLicences[comparisonLicenceNumber!].Add(naldLine);
            }
        }
        
        var naldLicenceStatusData = await naldLicenceStatusDataTask;
        ConsoleHelper.WriteLine("Finished getting nald licence status");
        
        var naldLiveLicenceDataByLowercasePermitNumber = new Dictionary<string, NaldAbstractionLicenceDataLine>();
        
        foreach (var licenceNumber in naldLicenceStatusData.LiveLicences)
        {
            var fullLicencePossibilities = comparableAbstractionLicences.SingleOrDefault(
                x => x.Key == licenceNumber);

            // If we didn't fetch data for the region, this could happen
            if (fullLicencePossibilities.Key == null)
            {
                continue;
            }
            
            var fullLicences = fullLicencePossibilities.Value
                .Where(x =>
                    (x.ExpiryDate == null || x.ExpiryDate > DateTime.Now)
                    && (x.RevDate == null || x.RevDate > DateTime.Now)
                    && (x.LapsedDate == null || x.LapsedDate > DateTime.Now));

            // TODO WA/055/0018/031 + WA/055/0018/31 both get compared to be the same here
            var fullLicence = fullLicences.First();
            
            var dmsStylePermitNumber = FormattingHelper.CleanPermitNumber(fullLicence.LicenceNo!).ToLower();
            naldLiveLicenceDataByLowercasePermitNumber.Add(dmsStylePermitNumber, fullLicence);
        }
        
        var pdfDataExtractors = new List<PdfDataExtractorService>();
        
        // Create a list of PdfDataExtractorService instances for parallel processing
        for (var serviceIdx = 1; serviceIdx <= maxConcurrentScrapers; serviceIdx++)
        {
            pdfDataExtractors.Add(
                CreatePdfDataExtractorService(
                    serviceIdx,
                    cacheService,
                    outputService,
                    pdfPigDocumentService,
                    docnetAlternativeDocumentService));
        }

        var fileServiceType = "api";

        IFileService fileService = fileServiceType switch
        {
            "api" => new ApiFileService(httpClient),
            _ => new LocalFileService("TODO")
        };
        
        ConsoleHelper.WriteLine("Started GetAndSaveLicenceReaderDataAsync");
        
        var outputLines = await GetAndSaveLicenceReaderDataAsync(
            pdfDataExtractors,
            fileService,
            cacheService,
            maxConcurrentScrapers,
            naldLiveLicenceDataByLowercasePermitNumber,
            await dmsExtractInfoTask,
            includeVersionMatch,
            delayPerProcessMs);

        ConsoleHelper.WriteLine("Finished GetAndSaveLicenceReaderDataAsync");        
        
        // Generate CSV report
        await ToolHelper.GenerateCsvReportWithSummaryAsync(
            outputLines,
            "LicenceReader",
            "Output",
            line => line.LicenceNumber ?? "No Licence Number scraped",
            "licence records",
            "Licence Processing Summary");
        
        var tsDuration = (DateTime.Now - dtStart).TotalSeconds;
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Completed in {tsDuration} seconds");

        return 1;
    }

    private static async Task<NaldDataCollection> GetAllNaldDataAsync(ICacheService cacheService, int take)
    {
        var allNaldData = new NaldDataCollection
        {
            AbstractionAndImpoundmentLicences = [],
            AbstractionLicencePoints = [],
            AbstractionLicencePurposes = [],
            AbstractionLicenceQuantities = [],
            AbstractionLicences = [],
            AbstractionLicenceVersions = []
        };

        var allNaldDataPartial = new NaldDataCollection();
        var loopIdx = 0;

        ConsoleHelper.WriteLine("Started getting all nald data");
        
        while (loopIdx == 0
               || allNaldDataPartial.AbstractionAndImpoundmentLicences!.Count == take
               || allNaldDataPartial.AbstractionLicencePoints!.Count == take
               || allNaldDataPartial.AbstractionLicencePurposes!.Count == take
               || allNaldDataPartial.AbstractionLicenceQuantities!.Count == take
               || allNaldDataPartial.AbstractionLicences!.Count == take
               || allNaldDataPartial.AbstractionLicenceVersions!.Count == take)
        {
            var skip = take * loopIdx++;
            ConsoleHelper.WriteLine($"Getting nald data - starting at {skip}");
            
            allNaldDataPartial = await cacheService.GetNaldDataAsync(null, false, skip, take);
            allNaldData.AbstractionAndImpoundmentLicences!.AddRange(allNaldDataPartial.AbstractionAndImpoundmentLicences!);
            allNaldData.AbstractionLicencePoints!.AddRange(allNaldDataPartial.AbstractionLicencePoints!);
            allNaldData.AbstractionLicencePurposes!.AddRange(allNaldDataPartial.AbstractionLicencePurposes!);
            allNaldData.AbstractionLicenceQuantities!.AddRange(allNaldDataPartial.AbstractionLicenceQuantities!);
            allNaldData.AbstractionLicences!.AddRange(allNaldDataPartial.AbstractionLicences!);
            allNaldData.AbstractionLicenceVersions!.AddRange(allNaldDataPartial.AbstractionLicenceVersions!);
        }
        
        return allNaldData;
    }
    
    private static async Task<Dictionary<string, List<DmsExtract>>> GetDmsExtractInfoAsync(
        ICacheService cacheService,
        int take)
    {
        ConsoleHelper.WriteLine("Started getting dms extracts");
        
        var dmsExtractInfoRaw = new List<DmsExtract>();

        List<DmsExtract> dmsExtractPartial = [];
        var loopIdx = 0;

        while (loopIdx == 0 || dmsExtractPartial.Count == take)
        {
            var skip = take * loopIdx++;
            ConsoleHelper.WriteLine($"Getting dms extracts - starting at {skip}");
            
            dmsExtractPartial = await cacheService.GetDmsExtractAsync(skip, take);
            dmsExtractInfoRaw.AddRange(dmsExtractPartial);
        }
        
        ConsoleHelper.WriteLine("Finished getting dms extracts");
        
        var dmsExtractInfo = new Dictionary<string, List<DmsExtract>>();

        foreach (var dmsRow in dmsExtractInfoRaw)
        {
            var permitNumberKey = dmsRow.PermitNumber.ToLower();
            
            if (dmsExtractInfo.TryGetValue(permitNumberKey, out var value))
            {
                value.Add(dmsRow);
                continue;
            }
            
            dmsExtractInfo.Add(permitNumberKey, [dmsRow]);
        }
        
        return dmsExtractInfo;
    }

    private static Task<MatchesResult> GetMatchesAsync(
        TemplateFinderInput fileMetadata,
        IPdfDataExtractorService pdfDataExtractor,
        LookupConfiguration configuration)
    {
        return pdfDataExtractor.GetMatchesAsync(
            fileMetadata.FileName!,
            new DmsFileData { FileId = fileMetadata.FileId },
            configuration,
            [fileMetadata.FileName!],
            0);
    }

    private static async Task<List<DmsFileReaderResult>> GetAndSaveLicenceReaderDataAsync(
        List<PdfDataExtractorService> pdfDataExtractors,
        IFileService fileService,
        ICacheService cacheService,
        int maxConcurrentScrapers,
        Dictionary<string, NaldAbstractionLicenceDataLine> naldLiveLicenceDataByLowercasePermitNumber,
        Dictionary<string, List<DmsExtract>> dmsExtractInfo,
        bool includeVersionMatch,
        int delayPerProcessMs)
    {
        var existingResults = await cacheService.GetDmsFileReaderResultsAsync();
        
        // NOTE - Next line for debugging only
        //existingResults.Clear();

        var redos = new List<Guid>();
        
        var processedFileIds = new HashSet<Guid>(
            existingResults
                .Where(existingResult => !redos.Contains(existingResult.FileId))
                .Select(existingResult => existingResult.FileId));
        
        var allPdfFilesInS3 = (await fileService.GetAllFilesWithMetadataAsync(string.Empty, int.MaxValue))
            .Where(fileMetadata => fileMetadata.Filename.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(fileMetadata => fileMetadata.Filename)
            .ToList();

        // TODO - Need to implement paging above
        
        var licenceFinderResultsRaw = await cacheService.GetLicenceFinderResultsAsync(0, int.MaxValue);
        var licenceFinderResultsByFileId = new Dictionary<Guid, List<LicenceFinderResult>>();
        
        foreach (var licenceFinderResult in licenceFinderResultsRaw)
        {
            if (string.IsNullOrWhiteSpace(licenceFinderResult.FileId))
            {
                continue;
            }

            if (!Guid.TryParse(licenceFinderResult.FileId!, out var fileId))
            {
                continue;
            }

            if (!licenceFinderResultsByFileId.TryAdd(fileId, [licenceFinderResult]))
            {
                licenceFinderResultsByFileId[fileId].Add(licenceFinderResult);
            }
        }
        
        // Create file entries from PDF files and filter out already processed ones
        var filesToProcessRaw = allPdfFilesInS3
            .Select(fileMetadata =>
            {
                var permitNumber = ExtractPermitNumber(fileMetadata.Filename);

                if (string.IsNullOrWhiteSpace(permitNumber))
                {
                    return null;
                }
                
                var fileId = ExtractFileId(fileMetadata.Filename);
                
                if (fileId == null)
                {
                    return null;
                }
                
                return new TemplateFinderInput
                {
                    FileName = fileMetadata.Filename,
                    PermitNumber = permitNumber,
                    FileId = fileId.Value,
                    FileSize = fileMetadata.Filesize
                };
            })
            .Where(templateFinderInputNullable => templateFinderInputNullable != null)
            .Select(templateFinderInputNullable => templateFinderInputNullable!)
            .Where(templateFinderInput => licenceFinderResultsByFileId.ContainsKey(templateFinderInput.FileId))
            .ToList();

        if (includeVersionMatch)
        {
            var versionFiles = await cacheService.GetVersionFilesAsync();

            foreach (var versionFile in versionFiles)
            {
                filesToProcessRaw.Add(new TemplateFinderInput
                { 
                    FileName = $"{versionFile.PermitNumber!.ToLower()}__{versionFile.FileId}.pdf",
                    PermitNumber = versionFile.PermitNumber,
                    FileId = versionFile.FileId!.Value,
                    FileSize = versionFile.FileSize!.Value
                });
            }
        }
        
        filesToProcessRaw = filesToProcessRaw
            .Where(templateFinderInput =>
                !processedFileIds.Contains(templateFinderInput.FileId)
                && !ExcludedFiles.Contains(templateFinderInput.FileName!)) // Comment out this line if debugging a certain file
            .ToList();
        
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Found {allPdfFilesInS3.Count} total PDF files at {DateTime.Now}");
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Found {licenceFinderResultsByFileId.Count} live licences to look at {DateTime.Now}");
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Already in CSV (completed or previously crashed): {existingResults.Count} files");

        var excludedCount = allPdfFilesInS3.Count(fileMetadata => ExcludedFiles.Contains(fileMetadata.Filename));
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Hard-coded exclusions: {excludedCount} files");

        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Remaining to process (with correct filenames etc..): {filesToProcessRaw.Count} files");

        if (filesToProcessRaw.Count == 0)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - All files have been processed. Returning existing results.");
            return existingResults;
        }
        
        // NOTE - Next line for debugging only - Filter to a subset of files if wanted
        /*filesToProcessRaw = filesToProcessRaw
            //.Where(fileMetadata =>
                //fileMetadata.FileId == Guid.Parse("907bb5a8-b735-440a-a9e9-0d49872d0ddd"))
            //.Skip(10)
            .Take(50)
            .ToList();*/
        
        await SetRunDateAsync(cacheService);
        
        var originalConfiguration = new LookupConfiguration(
            LicenceReaderConfiguration.GetLabels(),
            DmsFileData,
            [], // Don't need
            await CompanyName.GetFirstNamesCsvFromFileAsync(),
            fileService,
            cacheService,
            -1,
            skipFileIfMoreThenPages: 25);
        
        ConsoleHelper.WriteLine($"DEBUG - {nameof(GenerateLicenceReaderExtract)} - Retrieved {originalConfiguration.Labels.Count} label groups from configuration");

        ConsoleHelper.WriteLine($"\n=== Processing {filesToProcessRaw.Count} files ===");
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Processing {maxConcurrentScrapers} documents at a time...\n");
        
        var filenameIdx = 1;
        
        var scrapingTasks = new List<Task<DmsFileReaderResult?>>();
        
        var returnList = new List<DmsFileReaderResult>();
        var extractorLock = new Lock();

        var templateDict = new Dictionary<int, TemplateTypeIdentifierService>
        {
            { 1, new TemplateTypeIdentifierService("TODO1") },
            { 2, new TemplateTypeIdentifierService("TODO2") }
        };
        
        var fileTypeService = new FileTypeIdentifierService();
        
        foreach (var fileToProcess in filesToProcessRaw)
        {
            if (delayPerProcessMs > 0)
            {
                await Task.Delay(delayPerProcessMs);
            }
            
            var lowercasePermitNumber = fileToProcess.PermitNumber!.ToLower();
            
            var configuration = originalConfiguration.Clone();
            var naldData = naldLiveLicenceDataByLowercasePermitNumber.GetValueOrDefault(lowercasePermitNumber);

            if (naldData == null)
            {
                Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - No nald data found for {lowercasePermitNumber}");
            }
            
            configuration.RegionId = naldData?.FgacRegionCode ?? -1;
            
            ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - *** Starting file: {fileToProcess.FileName}" +
                $" (File {filenameIdx++} of {filesToProcessRaw.Count}) ***");
            
            scrapingTasks.Add(
                ScrapeDocumentAsync(
                    fileToProcess,
                    configuration,
                    pdfDataExtractors,
                    extractorLock,
                    templateDict[configuration.RegionId],
                    fileTypeService,
                    dmsExtractInfo,
                    cacheService));
            
            if (scrapingTasks.Count != maxConcurrentScrapers)
            {
                continue;
            }

            while (scrapingTasks.Count >= maxConcurrentScrapers)
            {
                await Task.WhenAny(scrapingTasks);
                var toRemoveList = new List<Task<DmsFileReaderResult?>>();
                
                // Check the others see if any completed (this might be superflous)
                foreach (var scrapingTask in scrapingTasks)
                {
                    if (!scrapingTask.IsCompleted)
                    {
                        continue;
                    }

                    var result = scrapingTask.Result;

                    if (result != null)
                    {
                        returnList.Add(result);
                    }

                    toRemoveList.Add(scrapingTask);
                }

                foreach (var toRemoveItem in toRemoveList)
                {
                    scrapingTasks.Remove(toRemoveItem);
                }
            }
        }
        
        // Finish any remaining
        foreach (var scrapingTask in scrapingTasks)
        {
            var result = await scrapingTask;

            if (result != null)
            {
                returnList.Add(result);                
            }
        }
        
        scrapingTasks.Clear();

        ConsoleHelper.WriteLine($"\nINFO - {nameof(GenerateLicenceReaderExtract)} - Completed processing all {filesToProcessRaw.Count} files.");

        // Combine existing results with newly processed results
        var allResults = existingResults
            .Concat(returnList)
            .ToList();
        
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Total results: {allResults.Count} (existing: {existingResults.Count}, new: {returnList.Count})");
        return allResults;
    }

    private static Task SetRunDateAsync(ICacheService cacheService)
    {
        return cacheService.SaveImportRunDateAsync("LicenceReaderExtract");
    }

    private static async Task<DmsFileReaderResult?> ScrapeDocumentAsync(
        TemplateFinderInput fileMetadata,
        LookupConfiguration configuration,
        List<PdfDataExtractorService> pdfDataExtractors,
        Lock extractorLock,
        TemplateTypeIdentifierService templateService,
        FileTypeIdentifierService fileTypeService,
        Dictionary<string, List<DmsExtract>> dmsExtractInfo,
        ICacheService cacheService)
    {
        IPdfDataExtractorService? pdfDataExtractor = null;
        
        try
        {
            var fileIdString = fileMetadata.FileId.ToString();
            var lowercasePermitNumber = fileMetadata.PermitNumber!.ToLowerInvariant();
            var dmsExtractHasPermitRow = dmsExtractInfo.ContainsKey(fileMetadata.PermitNumber!);
            
            var originalFileName = dmsExtractHasPermitRow
                ? dmsExtractInfo[lowercasePermitNumber]
                    .FirstOrDefault(dmsFile => dmsFile.FileId.Equals(fileIdString, StringComparison.InvariantCultureIgnoreCase))
                    ?.FileName
                : null;
            
            if (ExcludeBasedOnFilename(originalFileName))
            {
                return new DmsFileReaderResult
                {
                    Status = "OK",
                    PermitNumber = fileMetadata.PermitNumber!,
                    FileName = fileMetadata.FileName,
                    OriginalFileName = originalFileName,
                    FileId = fileMetadata.FileId,
                    FileType = "Excluded",
                    FileSize = fileMetadata.FileSize
                };
            }

            lock (extractorLock)
            {
                pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
                pdfDataExtractor.InUse = true;
            }
            
            MatchesResult internalJson;

            try
            {
                internalJson = await GetMatchesAsync(
                    fileMetadata,
                    pdfDataExtractor,
                    configuration);

                ConsoleHelper.WriteLine(
                    $"INFO - Generate licence reader extract - PDF extraction completed successfully for {fileMetadata.FileName} at {DateTime.Now}");
            }
            catch (TooManyPagesException tex)
            {
                var tooManyPagesResult = new DmsFileReaderResult
                {
                    Status = "Skipped",
                    ErrorMessage = tex.ToString(),
                    PermitNumber = fileMetadata.PermitNumber!,
                    FileName = fileMetadata.FileName,
                    FileId = fileMetadata.FileId,
                    NumberOfPages = tex.NumberOfPages
                };

                await cacheService.SaveDmsFileReaderResultAsync(tooManyPagesResult);
                return null;
            }
            catch (TooManyImagesException tex)
            {
                var tooManyPagesResult = new DmsFileReaderResult
                {
                    Status = "Skipped",
                    ErrorMessage = tex.ToString(),
                    PermitNumber = fileMetadata.PermitNumber!,
                    FileName = fileMetadata.FileName,
                    FileId = fileMetadata.FileId,
                    NumberOfPages = tex.NumberOfPages
                };

                await cacheService.SaveDmsFileReaderResultAsync(tooManyPagesResult);
                return null;
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(GenerateLicenceReaderExtract)} - Scraping file {fileMetadata.FileName}:");
                ConsoleHelper.WriteLine($"  Exception Type: {ex.GetType().Name}");
                ConsoleHelper.WriteLine($"  Message: {ex.Message}");
                ConsoleHelper.WriteLine($"  Stack Trace: {ex.StackTrace}");

                if (ex.InnerException != null)
                {
                    ConsoleHelper.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                    ConsoleHelper.WriteLine($"  Inner Message: {ex.InnerException.Message}");
                }
                
                var failedResult = new DmsFileReaderResult
                {
                    Status = "Error",
                    ErrorMessage = ex.ToString(),
                    PermitNumber = fileMetadata.PermitNumber!,
                    FileName = fileMetadata.FileName,
                    FileId = fileMetadata.FileId
                };

                await cacheService.SaveDmsFileReaderResultAsync(failedResult);
                return null;
            }
            
            // Extract licence number and date of issue from matches
            var licenceNumber = RuleSharedHelper.ExtractLicenceNumber(internalJson);
            var dateOfIssue = RuleSharedHelper.ExtractDateOfIssue(internalJson);

            ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Extracted - " +
                $"File: {fileMetadata.FileName}, Licence: {licenceNumber}, Date: {dateOfIssue}, Permit: {fileMetadata.PermitNumber}");

            var datetime = Date.GetDateOrNull(Date.DateFormatConsistent(dateOfIssue));
            var ruleResult = templateService.IdentifyTemplateType(internalJson);
            
            ConsoleHelper.WriteLine($"INFO - GenerateLicenceReaderExtract - Template identification completed for {fileMetadata.FileName}");
            const string unknownKey = "Unknown";
            
            var fileType = fileTypeService.IdentifyFileType(internalJson, originalFileName ?? fileMetadata.FileName!);
            
            var result = new DmsFileReaderResult
            {
                Status = "OK",
                LicenceNumber = licenceNumber,
                PermitNumber = fileMetadata.PermitNumber!,
                DateOfIssue = datetime,
                FileName = fileMetadata.FileName,
                OriginalFileName = originalFileName,
                FileId = fileMetadata.FileId,
                NumberOfPages = internalJson.NumberOfPages,
                PrimaryType = !string.IsNullOrEmpty(ruleResult?.TemplateType) ? ruleResult.TemplateType : unknownKey,
                SecondaryType = !string.IsNullOrEmpty(ruleResult?.Template) ? ruleResult.Template : unknownKey,
                FileType = fileType?.FileType ?? "Unknown",
                Confidence = fileType?.Confidence ?? 0.0,
                IdentifiedByRule = fileType?.IdentifiedByRule ?? "N/A",
                MatchedTerms = fileType?.MatchedTerms != null ? string.Join("; ", fileType.MatchedTerms) : string.Empty,
                FileSize = fileMetadata.FileSize
            };

            await cacheService.SaveDmsFileReaderResultAsync(result);
            return result;
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"ERROR - {nameof(GenerateLicenceReaderExtract)} - Processing file {fileMetadata.FileName}:");
            ConsoleHelper.WriteLine($"  Exception Type: {ex.GetType().Name}");
            ConsoleHelper.WriteLine($"  Message: {ex.Message}");
            ConsoleHelper.WriteLine($"  Stack Trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                ConsoleHelper.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                ConsoleHelper.WriteLine($"  Inner Message: {ex.InnerException.Message}");
            }
            
            var failedResult = new DmsFileReaderResult
            {
                Status = "Error",
                ErrorMessage = ex.ToString(),
                PermitNumber = fileMetadata.PermitNumber!,
                FileName = fileMetadata.FileName,
                FileId = fileMetadata.FileId
            };

            await cacheService.SaveDmsFileReaderResultAsync(failedResult);
            return null;
        }
        finally
        {
            if (pdfDataExtractor != null)
            {
                pdfDataExtractor.InUse = false;
            }
        }
    }

    private static bool ExcludeBasedOnFilename(string? filename)
    {
        if (string.IsNullOrEmpty(filename))
        {
            return false;
        }
        
        // Define terms to exclude
        var fileTypeExcludeTerms = new[]
        {
            "letter", 
            "WR51", 
            "determination", 
            "warning of further restrictions",
            "Environment Act 2028 - Booklet",
            "invoice",
            "inspection report",
            "technical report",
            "tech report",
            "83743S0057__8-37-43-S-0057Plans.pdf",
            "73412s0067__Scanned licence file plans upto 2012 8977701.pdf",
            "AN0330053090R01__Application Renewal Licence Issued - [Issued 18 10 2024] - 11 10 2024",
            "43004s0022__4-30-04-S-0022 6084539.PDF"
        };

        return fileTypeExcludeTerms
            .Any(term => filename.Contains(term, StringComparison.InvariantCultureIgnoreCase));;
    }
    
    private static string? ExtractPermitNumber(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return string.Empty;
        }

        var underscoreIndex = fileName.IndexOf("__", StringComparison.Ordinal);
        
        return underscoreIndex >= 0 
            ? fileName[..underscoreIndex].Trim() 
            : null;
    }
    
    private static Guid? ExtractFileId(string fileName)
    {
        if (string.IsNullOrEmpty(fileName))
        {
            return null;
        }

        var filenameParts = fileName.Split("__");
        var fileIdWithExtension = filenameParts.LastOrDefault()?.Trim();
        
        var fileIdString = fileIdWithExtension!.Split('.')[0];
        
        return Guid.TryParse(fileIdString, out var fileIdOut)
            ? fileIdOut
            : null;
    }
}