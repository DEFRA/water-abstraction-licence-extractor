using System.Collections.Concurrent;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Config;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools;

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

    private static string GetResultsCsvPath(string pdfFolder) => Path.Combine(pdfFolder, ResultsCsvFileName);

    private static List<LicenceReaderCsvLine> LoadExistingResults(string pdfFolder)
    {
        var csvPath = GetResultsCsvPath(pdfFolder);
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
                        DateOfIssue = string.IsNullOrEmpty(parts[3]) || parts[3] == "\"\"" ? null : parts[3].Trim('"')
                    });
                }
            }

            Console.WriteLine($"Loaded {results.Count} existing results from CSV (including files that were processing when crashed).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading existing results CSV: {ex.Message}");
            Console.WriteLine("Starting fresh.");
        }

        return results;
    }

    private static void MarkFileAsProcessingInCsv(string fileName, string pdfFolder)
    {
        var csvPath = GetResultsCsvPath(pdfFolder);
        
        lock (CsvWriteLock)
        {
            try
            {
                bool fileExists = File.Exists(csvPath);
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

    private static void UpdateFileResultInCsv(LicenceReaderCsvLine result, string pdfFolder)
    {
        var csvPath = GetResultsCsvPath(pdfFolder);
        
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
                    if (!lines[i].StartsWith($"\"{result.FileName}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                    
                    lines[i] = $"\"{result.FileName}\",\"{result.PermitNumber}\",\"{result.LicenceNumber}\",\"{result.DateOfIssue}\",\"Completed\"";

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
            pdfFolder);
    }

    public static async Task GenerateLicenceReaderExtractAsync(string pdfFolder, int regionCode)
    {
        var postgresDataSourceProvider = new NpgsqlDataSourceProvider(
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);
    
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
        var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);

        LicenceNumber.Instance = new LicenceNumber(databaseReadService);
        
        var cacheService = new DatabaseCacheService(
            databaseReadService,
            databaseAddService,
            KeyConfig.PostgresHost,
            KeyConfig.PostgresPort,
            KeyConfig.PostgresDbName,
            KeyConfig.PostgresUsername,
            KeyConfig.PostgresPassword);
        
        var outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
        
        const int batchSize = 10;
        var pdfDataExtractors = new List<PdfDataExtractorService>();
        
        // Create a list of PdfDataExtractorService instances for parallel processing
        for (var serviceIdx = 1; serviceIdx <= batchSize; serviceIdx++)
        {
            pdfDataExtractors.Add(
                CreatePdfDataExtractorService(
                    serviceIdx,
                    cacheService,
                    outputService,
                    pdfFolder));
        }

        var data = await GetLicenceReaderDataAsync(
            pdfDataExtractors,
            pdfFolder,
            regionCode);

        // Generate CSV report
        await ToolHelper.GenerateCsvReportWithSummaryAsync(
            data,
            "LicenceReader",
            OutputFolder,
            line => line.LicenceNumber ?? "No Licence",
            "licence records",
            "Licence Processing Summary");
    }

    private static async Task<MatchesResult> GetMatchesAsync(
        string fileName,
        string pdfFolder,
        PdfDataExtractorService pdfDataExtractor,
        LookupConfiguration configuration)
    {
        try
        {
            Console.WriteLine("Configuration created, calling PDF extractor...");

            var result = await pdfDataExtractor.GetMatchesAsync(
                pdfFolder + fileName,
                configuration,
                [pdfFolder + fileName],
                0);
            
            Console.WriteLine($"PDF extraction completed successfully for {fileName}");
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error in GetMatchesAsync for {fileName}:");
            Console.WriteLine($"  Exception Type: {ex.GetType().Name}");
            Console.WriteLine($"  Message: {ex.Message}");
            
            throw; // Re-throw to maintain the original error handling flow
        }
    }

    private static async Task<List<LicenceReaderCsvLine>> GetLicenceReaderDataAsync(
        List<PdfDataExtractorService> pdfDataExtractors,
        string pdfFolder,
        int regionCode)
    {
        // Load existing results (includes both completed and crashed files)
        var existingResults = LoadExistingResults(pdfFolder);
        
        var processedFileNames = new HashSet<string>(
            existingResults.Select(existingResult => existingResult.FileName)!,
            StringComparer.OrdinalIgnoreCase);

        var allPdfFileNames = Directory
            .GetFiles(pdfFolder)
            .Where(filePath => filePath.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Select(filePath => filePath.Split('/').Last())
            .OrderBy(fileName => fileName).ToList();

        // Filter out files already in CSV (completed or crashed) and hard-coded excluded files
        var pdfFileNames = allPdfFileNames
            .Where(fileName => !processedFileNames.Contains(fileName) && !ExcludedFiles.Contains(fileName))
            .ToList();

        Console.WriteLine($"Found {allPdfFileNames.Count} total PDF files");
        Console.WriteLine($"Already in CSV (completed or previously crashed): {existingResults.Count} files");

        var excludedCount = allPdfFileNames.Count(fileName => ExcludedFiles.Contains(fileName));
        Console.WriteLine($"Hard-coded exclusions: {excludedCount} files");

        Console.WriteLine($"Remaining to process: {pdfFileNames.Count} files");

        if (pdfFileNames.Count == 0)
        {
            Console.WriteLine("All files have been processed. Returning existing results.");
            
            return existingResults
                .OrderBy(existingResult => existingResult.PermitNumber)
                .ToList();
        }

        var labels = LicenceReaderConfiguration.GetLabels();
        Console.WriteLine($"Retrieved {labels.Count} label groups from configuration");
        
        var configuration = new LookupConfiguration(
            labels,
            FileLicenceMapping,
            [],
            regionCode);
        
        var returnList = new ConcurrentBag<LicenceReaderCsvLine>();
        var batchSize = pdfDataExtractors.Count;

        Console.WriteLine($"Processing in parallel batches of {batchSize}...");

        // Loop that goes up in 10s (or whatever batch size is)
        for (var filenameIdx = 0; filenameIdx < pdfFileNames.Count; filenameIdx += batchSize)
        {
            var filesBatch = pdfFileNames
                .Skip(filenameIdx)
                .Take(batchSize)
                .ToList();
            
            var batchNumber = (filenameIdx / batchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)pdfFileNames.Count / batchSize);

            Console.WriteLine($"\n=== Processing Batch {batchNumber} of {totalBatches} " +
                $"({filesBatch.Count} files in parallel) ===");

            // Create tasks for parallel processing
            var batchTasks = filesBatch.Select(async (pdfFilePath, indexInBatch) =>
            {
                // Use a dedicated PdfDataExtractorService instance for this parallel task
                var dedicatedExtractor = pdfDataExtractors[indexInBatch];

                try
                {
                    var fileNumber = filenameIdx + indexInBatch + 1;
                    
                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Starting file: {pdfFilePath}" +
                        $"(File {fileNumber} of {pdfFileNames.Count}) using extractor instance {indexInBatch}");

                    // Mark file as processing in CSV BEFORE we start
                    // If Tesseract crashes, this file will be in CSV and skipped on restart
                    MarkFileAsProcessingInCsv(pdfFilePath, pdfFolder);

                    // Check if file exists
                    var fullPath = pdfFolder + pdfFilePath;
                    
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"PDF file not found: {fullPath}");
                    }
 
                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] File exists, attempting " +
                        $"to extract matches for {pdfFilePath}...");
                    
                    var internalJson = await GetMatchesAsync(
                        pdfFilePath,
                        pdfFolder,
                        dedicatedExtractor,
                        configuration);

                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Matches extracted " +
                        $"successfully, processing licence data for {pdfFilePath}...");

                    // Extract licence number and date of issue from matches
                    var licenceNumber = SharedHelper.ExtractLicenceNumber(internalJson);
                    var dateOfIssue = SharedHelper.ExtractDateOfIssue(internalJson);

                    // Extract permit number from filename (everything before first underscore)
                    var permitNumber = SharedHelper.ExtractPermitNumberFromFilename(pdfFilePath);

                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Extracted - File: {pdfFilePath}, Licence: {licenceNumber}, Date: {dateOfIssue}, Permit: {permitNumber}");

                    var result = new LicenceReaderCsvLine
                    {
                        LicenceNumber = licenceNumber,
                        PermitNumber = permitNumber,
                        DateOfIssue = SharedHelper.DateFormatConsistent(dateOfIssue), 
                        FileName = pdfFilePath
                    };

                    returnList.Add(result);

                    // Update the CSV row with actual results
                    UpdateFileResultInCsv(result, pdfFolder);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Error processing file {pdfFilePath}:");
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

                    returnList.Add(failedResult);

                    // Update CSV with failed result
                    UpdateFileResultInCsv(failedResult, pdfFolder);
                }
            }).ToArray();

            // Wait for all tasks in the batch to complete
            await Task.WhenAll(batchTasks);

            Console.WriteLine($"Completed batch {batchNumber} of {totalBatches}. Processed {returnList.Count} files so far.");
        }

        Console.WriteLine($"\nCompleted processing all {pdfFileNames.Count} files in {Math.Ceiling((double)pdfFileNames.Count / batchSize)} parallel batches.");

        // Combine existing results with newly processed results
        var allResults = existingResults
            .Concat(returnList)
            .ToList();
        
        Console.WriteLine($"Total results: {allResults.Count} (existing: {existingResults.Count}, new: {returnList.Count})");

        return allResults
            .OrderBy(line => line.PermitNumber)
            .ToList();
    }
}