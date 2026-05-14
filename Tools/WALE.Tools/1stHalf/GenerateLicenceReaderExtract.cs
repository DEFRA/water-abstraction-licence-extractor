using System.Net;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
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
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tesseract;
using WALE.Tools.Config;
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

    public static async Task<int> GenerateLicenceReaderExtractAsync(string localPdfFolder)
    {
        var dtStart = DateTime.Now;

        #pragma warning disable SYSLIB0014
        ServicePointManager.DefaultConnectionLimit = 100;
        #pragma warning restore SYSLIB0014
    
        var clientHandler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
    
        var httpClient = new HttpClient(clientHandler);
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);
    
        var cacheService = new ApiCacheService(httpClient);
        var outputService = new ApiOutputService(httpClient);
        
        var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        
        var allNaldData = await cacheService.GetNaldDataAsync(null);
        LicenceNumber.Instance = new LicenceNumber(allNaldData.AbstractionAndImpoundmentLicences!);

        var naldLicenceDataByPermitNumber = new Dictionary<string, NaldAbstractionLicenceDataLine>();
        
        foreach (var line in allNaldData.AbstractionLicences!)
        {
            var dmsStylePermitNumber = FormattingHelper.CleanPermitNumber(line.LicenceNo!);

            if (!naldLicenceDataByPermitNumber.TryAdd(dmsStylePermitNumber, line))
            {
                // TODO log? - Ignore for now - we have ~3 collisions so its edge case
            }
        }

        var maxConcurrentScrapers = 10;
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
            _ => new LocalFileService(localPdfFolder)
        };

        var dmsExtractInfoRaw = await cacheService.GetDmsExtractAsync();
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
        
        await GetAndSaveLicenceReaderDataAsync(
            pdfDataExtractors,
            fileService,
            cacheService,
            maxConcurrentScrapers,
            naldLicenceDataByPermitNumber,
            dmsExtractInfo);

        var tsDuration = (DateTime.Now - dtStart).TotalSeconds;
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Completed in {tsDuration} seconds");

        return 1;
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

    private static async Task GetAndSaveLicenceReaderDataAsync(
        List<PdfDataExtractorService> pdfDataExtractors,
        IFileService fileService,
        ICacheService cacheService,
        int maxConcurrentScrapers,
        Dictionary<string, NaldAbstractionLicenceDataLine> naldLicenceDataByPermitNumber,
        Dictionary<string, List<DmsExtract>> dmsExtractInfo)
    {
        var existingResults = await cacheService.GetDmsFileReaderResultsAsync();
        
        // NOTE - Next line for debugging only
        //existingResults.Clear();
        
        var processedFileIds = new HashSet<Guid>(
            existingResults.Select(existingResult => existingResult.FileId));

        var allPdfFiles = (await fileService.GetAllFilesWithMetadataAsync())
            .Where(fileMetadata => fileMetadata.Filename.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .OrderBy(fileMetadata => fileMetadata.Filename)
            .ToList();
        
        // Create file entries from PDF files and filter out already processed ones
        var filesToProcessRaw = allPdfFiles
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
            .Where(templateFinderInput =>
                !processedFileIds.Contains(templateFinderInput!.FileId)
                && !ExcludedFiles.Contains(templateFinderInput.FileName!)) // Comment out this line if debugging a certain file
            .Select(templateFinderInputNullable => templateFinderInputNullable!)
            .ToList();

        var filesToProcessByPermitNumber = new Dictionary<string, List<TemplateFinderInput>>();

        foreach (var file in filesToProcessRaw)
        {
            if (!filesToProcessByPermitNumber.TryAdd(file.PermitNumber!, [file]))
            {
                filesToProcessByPermitNumber[file.PermitNumber!].Add(file);
            }
        }
        
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Found {allPdfFiles.Count} total PDF files at {DateTime.Now}");
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Already in CSV (completed or previously crashed): {existingResults.Count} files");

        var excludedCount = allPdfFiles.Count(fileMetadata => ExcludedFiles.Contains(fileMetadata.Filename));
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Hard-coded exclusions: {excludedCount} files");

        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Remaining to process (with correct filenames etc..): {filesToProcessByPermitNumber.Count} files");

        if (filesToProcessByPermitNumber.Count == 0)
        {
            ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - All files have been processed. Returning existing results.");
            return;
        }
        
        var filesToProcess = new List<TemplateFinderInput>();
        
        foreach (var naldLicencePermitNumberData in naldLicenceDataByPermitNumber)
        {
            if (!filesToProcessByPermitNumber.TryGetValue(naldLicencePermitNumberData.Key, out var value))
            {
                continue;
            }
            
            filesToProcess.AddRange(value);
        }
        
        // NOTE - Next line for debugging only - Filter to a subset of files if wanted
        filesToProcess = filesToProcess
            //.Where(fileMetadata =>
                //fileMetadata.FileId == Guid.Parse("1b7180e5-9949-40f4-92ee-d0171b05a8b7"))
            .Take(10)
            .ToList();
        
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

        ConsoleHelper.WriteLine($"\n=== Processing {filesToProcess.Count} files ===");
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Processing {maxConcurrentScrapers} documents at a time...\n");
        
        var filenameIdx = 1;
        
        var scrapingTasks = new List<Task<DmsFileReaderResult?>>();
        var minimumToFreeUp = maxConcurrentScrapers / 3;
        
        var returnList = new List<DmsFileReaderResult>();
        var extractorLock = new Lock();

        var templateService = new TemplateTypeIdentifierService("TODO");
        var fileTypeService = new FileTypeIdentifierService();
            
        foreach (var fileToProcess in filesToProcess)
        {
            var configuration = originalConfiguration.Clone();
            var naldData = naldLicenceDataByPermitNumber.ContainsKey(fileToProcess.PermitNumber!)
                ? naldLicenceDataByPermitNumber[fileToProcess.PermitNumber!]
                : null;

            configuration.RegionCode = naldData?.FgacRegionCode ?? -1;
            
            ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Starting file: {fileToProcess.FileName}" +
                $"(File {filenameIdx++} of {filesToProcess.Count})");
            
            scrapingTasks.Add(
                ScrapeDocumentAsync(
                    fileToProcess,
                    configuration,
                    pdfDataExtractors,
                    extractorLock,
                    templateService,
                    fileTypeService,
                    dmsExtractInfo,
                    cacheService));
            
            if (scrapingTasks.Count != maxConcurrentScrapers)
            {
                continue;
            }

            while (scrapingTasks.Count > maxConcurrentScrapers - minimumToFreeUp)
            {
                var finishedTask = await Task.WhenAny(scrapingTasks);
                var result = await finishedTask;

                if (result != null)
                {
                    returnList.Add(result);    
                }
                
                scrapingTasks.Remove(finishedTask);
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

        ConsoleHelper.WriteLine($"\nINFO - {nameof(GenerateLicenceReaderExtract)} - Completed processing all {filesToProcess.Count} files.");

        // Combine existing results with newly processed results
        var allResults = existingResults
            .Concat(returnList)
            .ToList();
        
        ConsoleHelper.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Total results: {allResults.Count} (existing: {existingResults.Count}, new: {returnList.Count})");
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
            lock (extractorLock)
            {
                pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
                pdfDataExtractor.InUse = true;
            }
            
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
            
            MatchesResult internalJson;

            try
            {
                internalJson = await GetMatchesAsync(
                    fileMetadata,
                    pdfDataExtractor,
                    configuration);
                
                ConsoleHelper.WriteLine($"INFO - Generate licence reader extract - PDF extraction completed successfully for {fileMetadata.FileName} at {DateTime.Now}");
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
            
            ConsoleHelper.WriteLine($"Template identification completed for {fileMetadata.FileName}");
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