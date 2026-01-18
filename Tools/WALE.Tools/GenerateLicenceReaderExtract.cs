using System.Collections.Concurrent;
using Tesseract;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Services.PdfPig;
using WALE.Tools.Config;
using WALE.Tools.Helpers;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class GenerateLicenceReaderExtract
{
    private static readonly string OutputFolder = KeyConfig.OutputFolder;
    private static readonly string CacheFolder = KeyConfig.CacheFolder;
    private static readonly Dictionary<string, string> FileLicenceMapping = new() {{"", ""}};
    private static readonly string ResultsCsvFileName = "licence_reader_processing_results.csv";
    private static readonly object CsvWriteLock = new object();

    // Hard-coded list of files to exclude from processing
    private static readonly HashSet<string> ExcludedFiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "42901G0004__4-29-01-G-0004 6079081.PDF",
        "42903G0001__4-29-03-G-0001 6079427.PDF",
        "42901G0001__Application Transfer Issued Licence 18.12.24.pdf",
        "42902S0004__4-29-02-S-0004 6079168.PDF",
        "42901S0033R01__Application Transfer Issued Licence 18.12.24.pdf"
    };

    private static string GetResultsCsvPath() => Path.Combine(KeyConfig.PdfFolder, ResultsCsvFileName);

    private static List<LicenceReaderCsvLine> LoadExistingResults()
    {
        var csvPath = GetResultsCsvPath();
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
            for (int i = 1; i < lines.Length; i++)
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

    private static void MarkFileAsProcessingInCsv(string fileName)
    {
        var csvPath = GetResultsCsvPath();
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

    private static void UpdateFileResultInCsv(LicenceReaderCsvLine result)
    {
        var csvPath = GetResultsCsvPath();
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
                bool updated = false;
                for (int i = 1; i < lines.Count; i++) // Skip header
                {
                    if (lines[i].StartsWith($"\"{result.FileName}\"", StringComparison.OrdinalIgnoreCase))
                    {
                        lines[i] = $"\"{result.FileName}\",\"{result.PermitNumber}\",\"{result.LicenceNumber}\",\"{result.DateOfIssue}\",\"Completed\"";
                        updated = true;
                        break;
                    }
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
        NpgsqlDataSourceProvider postgresDataSourceProvider, int id)
    {
        var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
        var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);

        var cacheService = new DatabaseCacheService(databaseReadService, databaseAddService, KeyConfig.PostgresConnectionString);
        var outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
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
                    outputService)
            },
            cacheService,
            outputService,
            KeyConfig.PdfFolder);
    }

    public static async Task GenerateLicenceReaderExtractAsync()
    {
        var postgresDataSourceProvider = new NpgsqlDataSourceProvider(KeyConfig.PostgresConnectionString);
    
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

        // Create a list of PdfDataExtractorService instances for parallel processing
        const int batchSize = 10;
        var pdfDataExtractors = new List<PdfDataExtractorService>();
        for (int i = 0; i < batchSize; i++)
        {
            pdfDataExtractors.Add(CreatePdfDataExtractorService(postgresDataSourceProvider, i + 1));
        }

        var data = await GetLicenceReaderDataAsync(pdfDataExtractors);

        // Generate CSV report using ToolHelper
        await ToolHelper.GenerateCsvReportWithSummaryAsync(
            data,
            "LicenceReader",
            OutputFolder,
            x => x.LicenceNumber ?? "No Licence",
            "licence records",
            "Licence Processing Summary");
    }

    static async Task<MatchesResult> GetMatchesAsync(string fileName, PdfDataExtractorService pdfDataExtractor)
    {
        var pdfFolder = KeyConfig.PdfFolder;

        try
        {
            Console.WriteLine($"Creating lookup configuration for {fileName}...");
            var labels = LicenceReaderConfiguration.GetLabels();
            Console.WriteLine($"Retrieved {labels.Count} label groups from configuration");

            var configuration = new LookupConfiguration(
                labels,
                FileLicenceMapping);

            Console.WriteLine($"Configuration created, calling PDF extractor...");

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

    static async Task<List<LicenceReaderCsvLine>> GetLicenceReaderDataAsync(List<PdfDataExtractorService> pdfDataExtractors)
    {
        // Load existing results (includes both completed and crashed files)
        var existingResults = LoadExistingResults();
        var processedFileNames = new HashSet<string>(existingResults.Select(x => x.FileName), StringComparer.OrdinalIgnoreCase);

        var allPdfFilePaths = Directory
            .GetFiles(KeyConfig.PdfFolder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => x.Split('/').Last())
            .OrderBy(x => x).ToList();

        // Filter out files already in CSV (completed or crashed) and hard-coded excluded files
        var pdfFilePaths = allPdfFilePaths
            .Where(fileName => !processedFileNames.Contains(fileName) && !ExcludedFiles.Contains(fileName))
            .ToList();

        var excludedCount = allPdfFilePaths.Count(fileName => ExcludedFiles.Contains(fileName));

        Console.WriteLine($"Found {allPdfFilePaths.Count} total PDF files");
        Console.WriteLine($"Already in CSV (completed or previously crashed): {existingResults.Count} files");
        Console.WriteLine($"Hard-coded exclusions: {excludedCount} files");
        Console.WriteLine($"Remaining to process: {pdfFilePaths.Count} files");

        if (pdfFilePaths.Count == 0)
        {
            Console.WriteLine("All files have been processed. Returning existing results.");
            return existingResults.OrderBy(x => x.PermitNumber).ToList();
        }

        var returnList = new ConcurrentBag<LicenceReaderCsvLine>();
        int batchSize = pdfDataExtractors.Count;

        Console.WriteLine($"Processing in parallel batches of {batchSize}...");

        for (int i = 0; i < pdfFilePaths.Count; i += batchSize)
        {
            var batch = pdfFilePaths.Skip(i).Take(batchSize).ToList();
            var batchNumber = (i / batchSize) + 1;
            var totalBatches = (int)Math.Ceiling((double)pdfFilePaths.Count / batchSize);

            Console.WriteLine($"\n=== Processing Batch {batchNumber} of {totalBatches} ({batch.Count} files in parallel) ===");

            // Create tasks for parallel processing
            var batchTasks = batch.Select(async (pdfFilePath, index) =>
            {
                // Use a dedicated PdfDataExtractorService instance for this parallel task
                var dedicatedExtractor = pdfDataExtractors[index];

                try
                {
                    var fileNumber = i + index + 1;
                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Starting file: {pdfFilePath} (File {fileNumber} of {pdfFilePaths.Count}) using extractor instance {index}");

                    // Mark file as processing in CSV BEFORE we start
                    // If Tesseract crashes, this file will be in CSV and skipped on restart
                    MarkFileAsProcessingInCsv(pdfFilePath);

                    // Check if file exists
                    var fullPath = KeyConfig.PdfFolder + pdfFilePath;
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"PDF file not found: {fullPath}");
                    }
 
                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] File exists, attempting to extract matches for {pdfFilePath}...");
                    var internalJson = await GetMatchesAsync(pdfFilePath, dedicatedExtractor);

                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Matches extracted successfully, processing licence data for {pdfFilePath}...");

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
                    UpdateFileResultInCsv(result);
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
                    UpdateFileResultInCsv(failedResult);
                }
            }).ToArray();

            // Wait for all tasks in the batch to complete
            await Task.WhenAll(batchTasks);

            Console.WriteLine($"Completed batch {batchNumber} of {totalBatches}. Processed {returnList.Count} files so far.");
        }

        Console.WriteLine($"\nCompleted processing all {pdfFilePaths.Count} files in {Math.Ceiling((double)pdfFilePaths.Count / batchSize)} parallel batches.");

        // Combine existing results with newly processed results
        var allResults = existingResults.Concat(returnList).ToList();
        Console.WriteLine($"Total results: {allResults.Count} (existing: {existingResults.Count}, new: {returnList.Count})");

        return allResults.OrderBy(x => x.PermitNumber).ToList();
    }

    
}

