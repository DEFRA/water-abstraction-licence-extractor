using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.RuleEngine.Helpers;
using WALE.ProcessFile.Services.AzureComputerVision;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
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
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly Dictionary<string, DmsFileData> FileLicenceMapping = [];
    private static readonly string ResultsCsvFileName = "licence_reader_processing_results.csv";
    private static readonly Lock CsvWriteLock = new();

    // Hard-coded list of files to exclude from processing
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "42901G0004__4-29-01-G-0004 6079081.PDF",
        "42903G0001__4-29-03-G-0001 6079427.PDF",
        "42901G0001__Application Transfer Issued Licence 18.12.24.pdf",
        "42902S0004__4-29-02-S-0004 6079168.PDF",
        "42901S0033R01__Application Transfer Issued Licence 18.12.24.pdf"
    };

    private static string GetInProgressResultsCsvPath(string pdfFolder) => Path.Combine(pdfFolder, ResultsCsvFileName);

    private static List<LicenceReaderCsvLine> LoadExistingResults(string pdfFolder)
    {
        var csvPath = GetInProgressResultsCsvPath(pdfFolder);
        var results = new List<LicenceReaderCsvLine>();

        if (!File.Exists(csvPath))
        {
            Console.WriteLine("No existing results CSV found. Starting fresh.");
            return results;
        }

        try
        {
            var lines = File.ReadAllLines(csvPath);
            
            if (lines.Length <= 1)
            {
                Console.WriteLine("Results CSV exists but is empty or has only header.");
                return results;
            }

            // Skip header line
            for (var i = 1; i < lines.Length; i++)
            {
                var parts = lines[i].Split(',');
                
                if (parts.Length >= 4)
                {
                    results.Add(new LicenceReaderCsvLine
                    {
                        FileName = parts[0].Trim('"'),
                        PermitNumber = parts[1].Trim('"'),
                        LicenceNumber = string.IsNullOrEmpty(parts[2]) || parts[2] == "\"\"" ? null : parts[2].Trim('"'),
                        DateOfIssue = string.IsNullOrEmpty(parts[3]) || parts[3] == "\"\"" ? null : DateOnly.Parse(parts[3].Trim('"')),
                        ProcessingStatus = parts[4].Trim('"'),
                    });
                }
            }

            Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} Loaded {results.Count} existing results from CSV (including files that were processing when crashed).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - {nameof(GenerateLicenceReaderExtract)} - loading existing results CSV: {ex.Message}");
            Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Starting fresh.");
        }

        return results;
    }

    private static void MarkFileAsProcessingInCsv(string fileName, string pdfFolder)
    {
        var csvPath = GetInProgressResultsCsvPath(pdfFolder);
        
        lock (CsvWriteLock)
        {
            try
            {
                var fileExists = File.Exists(csvPath);
                using var writer = new StreamWriter(csvPath, true);

                // Write header if file doesn't exist
                if (!fileExists)
                {
                    writer.WriteLine("FileName,PermitNumber,LicenceNumber,DateOfIssue,ProcessingStatus");
                }

                // Write placeholder row for file being processed
                writer.WriteLine($"\"{fileName}\",\"\",\"\",\"\",\"Processing\"");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error marking file as processing in CSV: {ex.Message}");
            }
        }
    }

    private static void UpdateInProgressFileResultsCsv(LicenceReaderCsvLine result, string pdfFolder, string status)
    {
        var csvPath = GetInProgressResultsCsvPath(pdfFolder);
        
        lock (CsvWriteLock)
        {
            try
            {
                if (!File.Exists(csvPath))
                {
                    Console.WriteLine($"Warning: CSV file does not exist when trying to update {result.FileName}");
                    return;
                }

                // Read all lines
                var lines = File.ReadAllLines(csvPath).ToList();

                // Find and update the row for this file
                var updated = false;
                
                for (var i = 1; i < lines.Count; i++) // Start at 1 to skip header
                {
                    var line = lines[i];
                    
                    if (!line.StartsWith($"\"{result.FileName}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    
                    lines[i] = $"\"{result.FileName}\",\"{result.PermitNumber}\",\"{result.LicenceNumber}\",\"{result.DateOfIssue}\",\"{status}\"";

                    updated = true;
                    break;
                }

                if (updated)
                {
                    File.WriteAllLines(csvPath, lines);
                }
                else
                {
                    Console.WriteLine($"Warning: Could not find row to update for {result.FileName}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error updating file result in CSV: {ex.Message}");
            }
        }
    }

    private static PdfDataExtractorService CreatePdfDataExtractorService(
        int id,
        ICacheService cacheService,
        IOutputService outputService,
        INoOcrPdfDocumentService documentService,
        string pdfFolder)
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
            pdfFolder);
    }

    public static async Task GenerateLicenceReaderExtractAsync(string pdfFolder, int regionCode)
    {
        var dtStart = DateTime.Now;
        
        var postgresDataSourceProvider = new NpgsqlDataSourceProvider(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);
    
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
        var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);
        
        var databaseCacheService = new DatabaseCacheService(
            databaseReadService,
            databaseAddService);
        
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);
    
        var apiCacheService = new ApiCacheService(httpClient);
        
        var cacheService = new MixedModeCacheService(apiCacheService, databaseCacheService);

        var databaseOutputService = new DatabaseOutputService(databaseReadService, databaseAddService);
        var apiOutputService = new ApiOutputService(httpClient);
        var outputService = new MixedModeOutputService(apiOutputService, databaseOutputService);
        
        var pdfPigDocumentService = new PdfPigNoOcrPdfDocumentService();
        
        var allNaldData = await cacheService.GetNaldDataAsync((short)regionCode);
        LicenceNumber.Instance = new LicenceNumber(allNaldData.LicencesAlternateFormat!);
        
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
                    pdfFolder));
        }

        var lines = await GetLicenceReaderDataAsync(
            pdfDataExtractors,
            pdfFolder,
            regionCode,
            maxConcurrentScrapers);

        // Generate CSV report
        await ToolHelper.GenerateCsvReportWithSummaryAsync(
            lines,
            "LicenceReader",
            OutputFolder,
            line => line.LicenceNumber ?? "No Licence Number scraped",
            "licence records",
            "Licence Processing Summary");

        var tsDuration = (DateTime.Now - dtStart).TotalSeconds;
        Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Completed in {tsDuration} seconds");
    }

    private static async Task<MatchesResult> GetMatchesAsync(
        string fileName,
        string pdfFolder,
        IPdfDataExtractorService pdfDataExtractor,
        LookupConfiguration configuration)
    {
        try
        {
            var result = await pdfDataExtractor.GetMatchesAsync(
                pdfFolder + fileName,
                configuration,
                [pdfFolder + fileName],
                0);
            
            Console.WriteLine($"INFO - Generate licence reader extract - PDF extraction completed successfully for {fileName} at {DateTime.Now}");
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetMatchesAsync for {fileName}:");
            Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            
            throw;
        }
    }

    private static async Task<List<LicenceReaderCsvLineWithoutStatus>> GetLicenceReaderDataAsync(
        List<PdfDataExtractorService> pdfDataExtractors,
        string pdfFolder,
        int regionCode,
        int maxConcurrentScrapers)
    {
        // Load existing results (includes both completed and crashed files) - bookmarking system
        var existingResults = LoadExistingResults(pdfFolder);
        
        // NOTE - Next line for debugging only
        //existingResults.Clear();
        
        var completedResults = existingResults
            .Where(er => er.ProcessingStatus == "Completed")
            .ToList();
        
        var processedFileNames = new HashSet<string>(
            completedResults.Select(existingResult => existingResult.FileName)!,
            StringComparer.OrdinalIgnoreCase);

        var allPdfFileNames = Directory
            .GetFiles(pdfFolder)
            .Where(filePath => filePath.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Select(filePath => filePath.Split('/').Last())
            .OrderBy(fileName => fileName)
            .ToList();
        
        // Filter out files already in CSV (completed or crashed) and hard-coded excluded files
        var pdfFileNames = allPdfFileNames
            .Where(fileName => !processedFileNames.Contains(fileName)
                && !ExcludedFiles.Contains(fileName)) // Comment out this line if debugging a certain file
            .ToList();
        
        // NOTE - Next line for debugging only - Filter to a subset of files if wanted
        pdfFileNames = pdfFileNames
            /*.Where(fileName =>
                fileName.StartsWith("42901G0003"))*/
            .Take(100)
            .ToList();
        
        Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Found {allPdfFileNames.Count} total PDF files at {DateTime.Now}");
        Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Already in CSV (completed or previously crashed): {existingResults.Count} files");

        var excludedCount = allPdfFileNames.Count(fileName => ExcludedFiles.Contains(fileName));
        Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Hard-coded exclusions: {excludedCount} files");

        Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Remaining to process: {pdfFileNames.Count} files");

        if (pdfFileNames.Count == 0)
        {
            Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - All files have been processed. Returning existing results.");
            
            return existingResults
                .OrderBy(existingResult => existingResult.PermitNumber)
                .Select(line => (LicenceReaderCsvLineWithoutStatus)line)
                .ToList();
        }
        
        var configuration = new LookupConfiguration(
            LicenceReaderConfiguration.GetLabels(),
            FileLicenceMapping,
            await CompanyName.GetFirstNamesCsvFromFileAsync(),
            regionCode);
        
        Console.WriteLine($"DEBUG - {nameof(GenerateLicenceReaderExtract)} - Retrieved {configuration.Labels.Count} label groups from configuration");

        Console.WriteLine($"\n=== Processing {pdfFileNames.Count} files ===");
        Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Processing {maxConcurrentScrapers} documents at a time...\n");
        
        var filenameIdx = 1;
        
        var scrapingTasks = new List<Task<LicenceReaderCsvLine>>();
        var minimumToFreeUp = maxConcurrentScrapers / 3;
        
        var returnList = new List<LicenceReaderCsvLine>();
        var extractorLock = new Lock();
        
        foreach (var pdfFileName in pdfFileNames)
        {
            Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Starting file: {pdfFileName}" +
                $"(File {filenameIdx++} of {pdfFileNames.Count})");
            
            scrapingTasks.Add(ScrapeDocumentAsync(pdfFileName, pdfFolder, configuration, pdfDataExtractors, extractorLock));
            
            if (scrapingTasks.Count != maxConcurrentScrapers)
            {
                continue;
            }

            while (scrapingTasks.Count > maxConcurrentScrapers - minimumToFreeUp)
            {
                var finishedTask = await Task.WhenAny(scrapingTasks);
                
                returnList.Add(await finishedTask);
                scrapingTasks.Remove(finishedTask);
            }
        }
        
        // Finish any remaining
        foreach (var scrapingTask in scrapingTasks)
        {
            returnList.Add(await scrapingTask);
        }
        
        scrapingTasks.Clear();

        Console.WriteLine($"\nINFO - {nameof(GenerateLicenceReaderExtract)} - Completed processing all {pdfFileNames.Count} files.");

        // Combine existing results with newly processed results
        var allResults = existingResults
            .Concat(returnList)
            .ToList();
        
        Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Total results: {allResults.Count} (existing: {existingResults.Count}, new: {returnList.Count})");

        return allResults
            .OrderBy(line => line.PermitNumber)
            .Select(line => (LicenceReaderCsvLineWithoutStatus)line)
            .ToList();
    }

    private static async Task<LicenceReaderCsvLine> ScrapeDocumentAsync(
        string pdfFilePath,
        string pdfFolder,
        LookupConfiguration configuration,
        List<PdfDataExtractorService> pdfDataExtractors,
        Lock extractorLock)
    {
        IPdfDataExtractorService? pdfDataExtractor = null;
        
        try
        {
            lock (extractorLock)
            {
                pdfDataExtractor = pdfDataExtractors.First(x => !x.InUse);
                pdfDataExtractor.InUse = true;
            }
            
            // Mark file as processing in CSV BEFORE we start
            // If Tesseract crashes, this file will be in CSV and skipped on restart
            MarkFileAsProcessingInCsv(pdfFilePath, pdfFolder);

            // Check if file exists
            var fullPath = pdfFolder + pdfFilePath;
            
            if (!File.Exists(fullPath))
            {
                throw new FileNotFoundException($"ERROR - {nameof(GenerateLicenceReaderExtract)} - PDF file not found: {fullPath}");
            }
            
            var internalJson = await GetMatchesAsync(
                pdfFilePath,
                pdfFolder,
                pdfDataExtractor,
                configuration);

            // Extract licence number and date of issue from matches
            var licenceNumber = RuleSharedHelper.ExtractLicenceNumber(internalJson);
            var dateOfIssue = RuleSharedHelper.ExtractDateOfIssue(internalJson);

            // Extract permit number from filename
            var permitNumber = SharedHelper.ExtractPermitNumberFromFilename(pdfFilePath);

            Console.WriteLine($"INFO - {nameof(GenerateLicenceReaderExtract)} - Extracted - " +
                $"File: {pdfFilePath}, Licence: {licenceNumber}, Date: {dateOfIssue}, Permit: {permitNumber}");

            var datetime = Date.GetDateOrNull(Date.DateFormatConsistent(dateOfIssue));
            var dateOnly = datetime != null ? DateOnly.FromDateTime(datetime.Value) : (DateOnly?)null;
            
            var result = new LicenceReaderCsvLine
            {
                LicenceNumber = licenceNumber,
                PermitNumber = permitNumber,
                DateOfIssue = dateOnly,
                FileName = pdfFilePath,
                ProcessingStatus = "Completed"
            };

            // Update the CSV row with actual results
            UpdateInProgressFileResultsCsv(result, pdfFolder, result.ProcessingStatus);

            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"ERROR - {nameof(GenerateLicenceReaderExtract)} - Processing file {pdfFilePath}:");
            Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            Console.WriteLine($"  Stack Trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                Console.WriteLine($"  Inner Exception: {ex.InnerException.GetType().Name}");
                Console.WriteLine($"  Inner Message: {ex.InnerException.Message}");
            }

            // Add entry with filename but null values to track failed files
            var failedResult = new LicenceReaderCsvLine
            {
                LicenceNumber = null,
                PermitNumber = SharedHelper.ExtractPermitNumberFromFilename(pdfFilePath),
                DateOfIssue = null,
                FileName = pdfFilePath
            };

            // Update CSV with failed result
            UpdateInProgressFileResultsCsv(failedResult, pdfFolder, "Failed");
            
            return failedResult;
        }
        finally
        {
            if (pdfDataExtractor != null)
            {
                pdfDataExtractor.InUse = false;
            }
        }
    }
}