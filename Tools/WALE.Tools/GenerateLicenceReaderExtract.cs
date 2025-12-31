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

    public static async Task GenerateLicenceReaderExtractAsync()
    {
        var postgresDataSourceProvider = new NpgsqlDataSourceProvider(KeyConfig.PostgresConnectionString);
    
        Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;
        var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
        var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);
    
        var cacheService = new DatabaseCacheService(databaseReadService, databaseAddService);
        var outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.SparseTextOsd, cacheService, outputService),
                new TesseractOcrDataExtractorService(KeyConfig.TesseractPrefix, PageSegMode.Auto, cacheService, outputService),
                new AzureAiVisionOcrDataExtractorService(
                    KeyConfig.AiVisionEndpoint,
                    KeyConfig.AiVisionKey,
                    cacheService,
                    outputService)
            },
            cacheService,
            outputService,
            KeyConfig.PdfFolder);

        var data = await GetLicenceReaderDataAsync(pdfDataExtractor);

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

    static async Task<List<LicenceReaderCsvLine>> GetLicenceReaderDataAsync(PdfDataExtractorService pdfDataExtractor)
    {
        var pdfFilePaths = Directory
            .GetFiles(KeyConfig.PdfFolder)
            .Where(fileName => fileName.EndsWith(".pdf", StringComparison.InvariantCultureIgnoreCase))
            .Select(x => x.Split('/').Last())
            .OrderBy(x => x).ToList();

        var returnList = new ConcurrentBag<LicenceReaderCsvLine>();
        const int batchSize = 14;

        Console.WriteLine($"Found {pdfFilePaths.Count} PDF files to process");
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
                try
                {
                    var fileNumber = i + index + 1;
                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Starting file: {pdfFilePath} (File {fileNumber} of {pdfFilePaths.Count})");

                    // Check if file exists
                    var fullPath = KeyConfig.PdfFolder + pdfFilePath;
                    if (!File.Exists(fullPath))
                    {
                        throw new FileNotFoundException($"PDF file not found: {fullPath}");
                    }

                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] File exists, attempting to extract matches for {pdfFilePath}...");
                    var internalJson = await GetMatchesAsync(pdfFilePath, pdfDataExtractor);

                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Matches extracted successfully, processing licence data for {pdfFilePath}...");

                    // Extract licence number and date of issue from matches
                    var licenceNumber = SharedHelper.ExtractLicenceNumber(internalJson);
                    var dateOfIssue = SharedHelper.ExtractDateOfIssue(internalJson);

                    // Extract permit number from filename (everything before first underscore)
                    var permitNumber = SharedHelper.ExtractPermitNumberFromFilename(pdfFilePath);

                    Console.WriteLine($"[Thread {Environment.CurrentManagedThreadId}] Extracted - File: {pdfFilePath}, Licence: {licenceNumber}, Date: {dateOfIssue}, Permit: {permitNumber}");

                    returnList.Add(new LicenceReaderCsvLine
                    {
                        LicenceNumber = licenceNumber,
                        PermitNumber = permitNumber,
                        DateOfIssue = SharedHelper.DateFormatConsistent(dateOfIssue), 
                        FileName = pdfFilePath
                    });
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
                    returnList.Add(new LicenceReaderCsvLine
                    {
                        LicenceNumber = null,
                        PermitNumber = SharedHelper.ExtractPermitNumberFromFilename(pdfFilePath),
                        DateOfIssue = null
                    });
                }
            }).ToArray();

            // Wait for all tasks in the batch to complete
            await Task.WhenAll(batchTasks);

            Console.WriteLine($"Completed batch {batchNumber} of {totalBatches}. Processed {returnList.Count} files so far.");
        }

        Console.WriteLine($"\nCompleted processing all {pdfFilePaths.Count} files in {Math.Ceiling((double)pdfFilePaths.Count / batchSize)} parallel batches.");
        return returnList.ToList().OrderBy(x => x.PermitNumber).ToList();
    }

    
}
