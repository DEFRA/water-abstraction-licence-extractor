using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Engine;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Configuration;
using CsvHelper;
using CsvHelper.Configuration;
using System.Globalization;

namespace WALE.ProcessFile.RuleEngine.Services;

/// <summary>
/// Service for identifying file types based on content analysis using PDF text extraction
/// </summary>
public class FileTypeIdentifierService
{
    private readonly IRuleEngine<FileTypeResult> _ruleEngine;
    private readonly List<IPdfDataExtractorService>? _pdfExtractorServices;
    private readonly Lock _lockObject = new();
    private int _currentServiceIndex;

    /// <summary>
    /// Initializes a new instance of FileTypeIdentifierService with PDF extractor service
    /// </summary>
    /// <param name="pdfExtractorServices">PDF extractor service with OCR support</param>
    public FileTypeIdentifierService(List<IPdfDataExtractorService> pdfExtractorServices)
    {
        _ruleEngine = new RuleEngine<FileTypeResult>();
        _pdfExtractorServices = pdfExtractorServices
            ?? throw new ArgumentNullException(nameof(pdfExtractorServices));
        
        InitializeDefaultRules();
    }

    /// <summary>
    /// Gets the page count of a PDF file without full processing
    /// </summary>
    /// <param name="filePath">The path to the PDF file</param>
    /// <param name="outputService">TODO</param>
    /// <returns>The number of pages in the PDF</returns>
    private static async Task<int> GetPageCountAsync(string filePath, IOutputService outputService)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File not found: {filePath}");
        }

        return await Task.Run(() =>
        {
            try
            {
                var pdfDocument = new PdfDocument(filePath, false, outputService); // TODO should this always load it?
                return pdfDocument.Pages.Count;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ERROR - {ex.Message}");
                
                // If we can't read the PDF, return 0 to exclude it
                return 0;
            }
        });
    }

    /// <summary>
    /// Identifies the file type based on the content of a file using OCR when needed
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <param name="configuration">The lookup configuration</param>
    /// <returns>The file type identification result, or null if no type could be identified or an error occurred</returns>
    public async Task<FileTypeResult?> IdentifyFileTypeAsync(string filePath, LookupConfiguration configuration)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                Console.WriteLine($"File not found: {filePath}");
                return null;
            }

            if (_pdfExtractorServices == null || _pdfExtractorServices.Count == 0)
            {
                Console.WriteLine("No PDF extractor services available");
                return null;
            }

            IPdfDataExtractorService serviceToUse;

            // Lock to ensure thread-safe access to the service list
            lock (_lockObject)
            {
                serviceToUse = _pdfExtractorServices[_currentServiceIndex % _pdfExtractorServices.Count];
                _currentServiceIndex++;
            }

            var content = await serviceToUse.GetMatchesAsync(
                filePath, configuration, [], 0);

            return _ruleEngine.Evaluate(content);
        }
        catch (Exception ex)
        {
            Console.WriteLine("ERROR - " + ex.Message);
            return null;
        }
    }

    /// <summary>
    /// CSV record class for tracking processed files
    /// </summary>
    private class ProcessedFileRecord
    {
        public string FilePath { get; set; } = string.Empty;
        public string FileType { get; set; } = string.Empty;
        public double Confidence { get; set; }
        public string ProcessedDate { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty; // "Success" or "Error"
        public string IdentifiedByRule { get; set; } = string.Empty;
        public List<string> MatchedTerms { get; set; } = [];
    
        public string? DateOfIssue { get; set; }
        public string? LicenceNumber { get; set; }
        public long FileSize { get; set; }
    }

    /// <summary>
    /// CSV record class for caching page counts
    /// </summary>
    private class PageCountRecord
    {
        public string FilePath { get; set; } = string.Empty;
        public int PageCount { get; set; }
        public string CheckedDate { get; set; } = string.Empty;
    }

    /// <summary>
    /// Gets the path to the CSV tracking file
    /// </summary>
    private static string GetCsvFilePath(string directoryPath)
    {
        return Path.Combine(directoryPath, "ProcessedFiles.csv");
    }

    /// <summary>
    /// Gets the path to the page count cache CSV file
    /// </summary>
    private static string GetPageCountCsvFilePath(string directoryPath)
    {
        return Path.Combine(directoryPath, "PageCounts.csv");
    }

    /// <summary>
    /// Reads cached page counts from CSV
    /// </summary>
    /// <param name="directoryPath">The directory path</param>
    /// <returns>A dictionary mapping file paths to their page counts</returns>
    private static Dictionary<string, int> ReadPageCountsFromCsv(string directoryPath)
    {
        var pageCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var csvPath = GetPageCountCsvFilePath(directoryPath);

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"Page count cache not found at {csvPath}. Will check all files.");
            return pageCounts;
        }

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            };

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, config);

            var records = csv.GetRecords<PageCountRecord>().ToList();

            // Group by filename and take only the first record for each file
            var uniqueRecords = records
                .Where(r => !string.IsNullOrEmpty(r.FilePath))
                .GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            foreach (var record in uniqueRecords)
            {
                pageCounts[record.FilePath] = record.PageCount;
            }

            Console.WriteLine($"Loaded {pageCounts.Count} cached page counts from CSV.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading page count cache: {ex.Message}. Will check all files.");
        }

        return pageCounts;
    }

    /// <summary>
    /// Writes page count results to cache CSV
    /// </summary>
    /// <param name="directoryPath">The directory path</param>
    /// <param name="newPageCounts">New page counts to append</param>
    private static void WritePageCountsToCsv(string directoryPath, Dictionary<string, int> newPageCounts)
    {
        var csvPath = GetPageCountCsvFilePath(directoryPath);

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            var fileExists = File.Exists(csvPath);

            using var stream = new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            using var csv = new CsvWriter(writer, config);

            // Write header if new file
            if (!fileExists)
            {
                csv.WriteHeader<PageCountRecord>();
                csv.NextRecord();
            }

            // Append new page counts
            foreach (var kvp in newPageCounts)
            {
                var record = new PageCountRecord
                {
                    FilePath = kvp.Key,
                    PageCount = kvp.Value,
                    CheckedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                };
                
                csv.WriteRecord(record);
                csv.NextRecord();
            }

            writer.Flush();
            Console.WriteLine($"Saved {newPageCounts.Count} page counts to cache.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing page counts to cache: {ex.Message}");
        }
    }

    /// <summary>
    /// Reads already processed files from the CSV tracking file
    /// </summary>
    /// <param name="directoryPath">The directory path</param>
    /// <returns>A dictionary of already processed files with their results</returns>
    private Dictionary<string, FileTypeResult?> ReadProcessedFilesFromCsv(string directoryPath)
    {
        var results = new Dictionary<string, FileTypeResult?>();
        var csvPath = GetCsvFilePath(directoryPath);

        if (!File.Exists(csvPath))
        {
            Console.WriteLine($"CSV tracking file not found at {csvPath}. Starting fresh.");
            return results;
        }

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null // Ignore missing headers for backward compatibility
            };

            using var reader = new StreamReader(csvPath);
            using var csv = new CsvReader(reader, config);

            var allRecords = csv.GetRecords<ProcessedFileRecord>().ToList();

            // Group by filename and take only the first record for each file
            var uniqueRecords = allRecords
                .Where(r => !string.IsNullOrEmpty(r.FilePath))
                .GroupBy(r => r.FilePath, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToList();

            var totalRecords = allRecords.Count;
            var duplicateCount = totalRecords - uniqueRecords.Count;

            if (duplicateCount > 0)
            {
                Console.WriteLine($"Found {duplicateCount} duplicate entries in CSV. Using first occurrence for each file.");
            }

            // Add all unique records to results regardless of status
            foreach (var record in uniqueRecords)
            {
                FileTypeResult? result = null;
                
                if (!string.IsNullOrEmpty(record.FileType) && record.FileType != "N/A")
                {
                    result = new FileTypeResult 
                    { 
                        FileType = record.FileType, 
                        Confidence = record.Confidence,
                        MatchedTerms = record.MatchedTerms,
                        IdentifiedByRule = record.IdentifiedByRule ?? string.Empty,
                        DateOfIssue = record.DateOfIssue ?? string.Empty,
                        LicenceNumber = record.LicenceNumber ?? string.Empty
                    };
                }
                
                results[record.FilePath] = result;
            }

            Console.WriteLine($"Loaded {uniqueRecords.Count} unique processed files from CSV (will be skipped).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading CSV file: {ex.Message}. Starting fresh.");
        }

        return results;
    }

    /// <summary>
    /// Writes error records to the CSV tracking file
    /// </summary>
    /// <param name="directoryPath">The directory path</param>
    /// <param name="errorRecords">Dictionary of file paths to error messages</param>
    private static void WriteErrorsToCsv(string directoryPath, Dictionary<string, string> errorRecords)
    {
        var csvPath = GetCsvFilePath(directoryPath);

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            var fileExists = File.Exists(csvPath);

            using var stream = new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            using var csv = new CsvWriter(writer, config);

            // Write header if new file
            if (!fileExists)
            {
                csv.WriteHeader<ProcessedFileRecord>();
                csv.NextRecord();
            }

            // Append error records
            foreach (var kvp in errorRecords)
            {
                var record = new ProcessedFileRecord
                {
                    FilePath = kvp.Key,
                    FileType = "N/A",
                    Confidence = 0.0,
                    ProcessedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "Error",
                    ErrorMessage = kvp.Value
                };
                
                csv.WriteRecord(record);
                csv.NextRecord();
            }

            writer.Flush();
            Console.WriteLine($"Logged {errorRecords.Count} errors to CSV at {csvPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing errors to CSV file: {ex.Message}");
        }
    }

    /// <summary>
    /// Writes or appends results to the CSV tracking file
    /// </summary>
    /// <param name="directoryPath">The directory path</param>
    /// <param name="newResults">New results to append</param>
    /// <param name="allResults">All results including previously processed</param>
    private static void WriteResultsToCsv(string directoryPath, Dictionary<string, FileTypeResult?> newResults)
    {
        var csvPath = GetCsvFilePath(directoryPath);

        try
        {
            var config = new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true
            };

            var fileExists = File.Exists(csvPath);

            using var stream = new FileStream(csvPath, FileMode.Append, FileAccess.Write, FileShare.Read);
            using var writer = new StreamWriter(stream);
            using var csv = new CsvWriter(writer, config);

            // Write header if new file
            if (!fileExists)
            {
                csv.WriteHeader<ProcessedFileRecord>();
                csv.NextRecord();
            }

            // Append new results
            foreach (var kvp in newResults)
            {
                var record = new ProcessedFileRecord
                {
                    FilePath = kvp.Key,
                    FileType = kvp.Value?.FileType ?? "Unknown",
                    Confidence = kvp.Value?.Confidence ?? 0.0,
                    ProcessedDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    Status = "Success",
                    ErrorMessage = string.Empty,
                    IdentifiedByRule = kvp.Value?.IdentifiedByRule ?? string.Empty,
                    DateOfIssue = kvp.Value?.DateOfIssue ?? string.Empty,
                    LicenceNumber = kvp.Value?.LicenceNumber ?? string.Empty,
                };
                
                csv.WriteRecord(record);
                csv.NextRecord();
            }

            writer.Flush();
            Console.WriteLine($"Saved {newResults.Count} new results to CSV at {csvPath}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error writing to CSV file: {ex.Message}");
        }
    }

    /// <summary>
    /// Processes all files in a directory and identifies their types
    /// </summary>
    /// <param name="directoryPath">The directory path to process</param>
    /// TODO other 2 params
    /// <param name="outputService"></param>
    /// <param name="searchPattern">File search pattern (default: "*.*")</param>
    /// <param name="lookupConfiguration"></param>
    /// <returns>A dictionary mapping file paths to their identification results</returns>
    public async Task<Dictionary<string, FileTypeResult?>> ProcessDirectoryAsync(
        string directoryPath,
        LookupConfiguration lookupConfiguration,
        IOutputService outputService,
        string searchPattern = "*.*")
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");
        }

        // Load already processed files from CSV
        var results = ReadProcessedFilesFromCsv(directoryPath);
        var processedFiles = new HashSet<string>(results.Keys, StringComparer.OrdinalIgnoreCase);

        var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).Distinct();

        // Define terms to exclude
        var excludeTerms = new[]
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

        // Filter out excluded files and already processed files
        var filteredFiles = files
          //  .Where(f => f.Contains("12202043__Licence - Signed Addendum 6431587"))
            .Where(file =>
            {
                var fileName = Path.GetFileName(file).ToLowerInvariant();
                return !excludeTerms.Any(term => fileName.Contains(term.ToLowerInvariant()));
            })
           .Where(file => !processedFiles.Contains(file))
            .ToList();

        if (filteredFiles.Count == 0)
        {
            Console.WriteLine("No new files to process. All files have already been processed.");
            return results;
        }

        Console.WriteLine($"Found {filteredFiles.Count} new files to process (excluding {processedFiles.Count} already processed files)");

        // Load cached page counts
        var cachedPageCounts = ReadPageCountsFromCsv(directoryPath);

        // Filter files by page count (less than 25 pages)
        var pageCountFilteredFiles = new List<string>();
        var filesToCheckPageCount = new List<string>();

        // First, use cached page counts
        foreach (var file in filteredFiles)
        {
            if (cachedPageCounts.TryGetValue(file, out var cachedCount))
            {
                if (cachedCount is > 0 and < 25)
                {
                    pageCountFilteredFiles.Add(file);
                }
                else
                {
                    Console.WriteLine($"Filtered out {Path.GetFileName(file)} - {cachedCount} pages (cached)");
                }
            }
            else
            {
                filesToCheckPageCount.Add(file);
            }
        }

        Console.WriteLine($"Using {pageCountFilteredFiles.Count} files from page count cache.");

        if (filesToCheckPageCount.Count > 0)
        {
            Console.WriteLine($"Checking page counts for {filesToCheckPageCount.Count} uncached files...");
            var pageCountStart = DateTime.Now;

            // Process page count checks in parallel batches
            const int pageCountBatchSize = 50;
            
            var pageCountBatches = filesToCheckPageCount
                .Select((file, index) => new { file, index })
                .GroupBy(x => x.index / pageCountBatchSize)
                .Select(g => g.Select(x => x.file).ToList())
                .ToList();

            for (var batchIndex = 0; batchIndex < pageCountBatches.Count; batchIndex++)
            {
                var batch = pageCountBatches[batchIndex];
                var batchPosition = batchIndex + 1;
                
                Console.WriteLine($"Checking page counts for batch {batchPosition}/{pageCountBatches.Count} ({batch.Count} files)...");

                var newPageCounts = new Dictionary<string, int>();

                var pageCountTasks = batch.Select(async file =>
                {
                    try
                    {
                        var pageCount = await GetPageCountAsync(file, outputService);
                        return new { File = file, PageCount = pageCount, Success = true };
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error getting page count for {Path.GetFileName(file)}: {ex.Message}");
                        return new { File = file, PageCount = 0, Success = false };
                    }
                });

                var batchResults = await Task.WhenAll(pageCountTasks);

                // Process results and cache them
                foreach (var result in batchResults)
                {
                    if (!result.Success)
                    {
                        continue;
                    }
                    
                    newPageCounts[result.File] = result.PageCount;

                    if (result.PageCount is > 0 and < 25)
                    {
                        pageCountFilteredFiles.Add(result.File);
                    }
                    else
                    {
                        Console.WriteLine($"Filtered out {Path.GetFileName(result.File)} - {result.PageCount} pages");
                    }
                }

                // Save page counts to cache immediately
                if (newPageCounts.Count > 0)
                {
                    WritePageCountsToCsv(directoryPath, newPageCounts);
                }
            }

            var pageCountDuration = (DateTime.Now - pageCountStart).TotalSeconds;
            Console.WriteLine($"Page count checking completed in {pageCountDuration:F2} seconds");
        }

        Console.WriteLine($"Processing {pageCountFilteredFiles.Count} files (filtered from {filteredFiles.Count} based on page count < 25)");
        filteredFiles = pageCountFilteredFiles;

        // Process files in batches of 10
        const int batchSize = 10;
        
        var batches = filteredFiles
            .Select((file, index) => new { file, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.file).ToList())
            .ToList();

        for (var batchIndex = 0; batchIndex < batches.Count; batchIndex++)
        {
            var batch = batches[batchIndex];
            var batchPosition = batchIndex + 1;
            Console.WriteLine($"Processing batch {batchPosition}/{batches.Count} ({batch.Count} files)...");
           
            var batchResultsDict = new Dictionary<string, FileTypeResult?>();
            var batchErrorsDict = new Dictionary<string, string>();

            var successCount = 0;
            var errorCount = 0;

            // Process files sequentially instead of in parallel to isolate Tesseract crashes
            foreach (var file in batch)
            {
                var fileName = Path.GetFileName(file);
                Console.WriteLine($"  Processing file: {fileName}");

                try
                {
                    // Use Task.Run with aggressive timeout to isolate native crashes
                    var timeoutMinutes = 3;
                    var cancellationTokenSource = new CancellationTokenSource();

                    var processTask = Task.Run(async () =>
                    {
                        try
                        {
                            var result = await IdentifyFileTypeAsync(file, configuration: lookupConfiguration);
                            return new { Success = true, Result = result, Error = (string?)null };
                        }
                        catch (Exception ex)
                        {
                            var errorType = "Unknown";
                            var errorMessage = ex.Message;

                            if (errorMessage.Contains("PAGE_RES_IT") || errorMessage.Contains("pageres.cpp") || 
                                errorMessage.Contains("Tesseract") || errorMessage.Contains("Assert failed"))
                            {
                                errorType = "Tesseract OCR Error";
                                errorMessage = $"OCR processing failed - file may be corrupted. Details: {errorMessage}";
                            }
                            else if (ex is OutOfMemoryException)
                            {
                                errorType = "Memory Error";
                                errorMessage = "Insufficient memory to process file";
                            }
                            else if (ex is FileNotFoundException || ex is IOException)
                            {
                                errorType = "File Access Error";
                            }

                            var csvErrorMessage = errorMessage.Length > 200 ? errorMessage.Substring(0, 200) + "..." : errorMessage;
                            Console.WriteLine($"ERROR - {errorType} ({fileName})");
                            
                            return new
                            {
                                Success = false,
                                Result = (FileTypeResult?)null,
                                Error = csvErrorMessage
                            }!;
                        }
                    }, cancellationTokenSource.Token);

                    // Wait with timeout
                    var completedTask = await Task.WhenAny(processTask, Task.Delay(TimeSpan.FromMinutes(timeoutMinutes), cancellationTokenSource.Token));

                    if (completedTask == processTask)
                    {
                        await cancellationTokenSource.CancelAsync(); // Cancel the delay task
                        var result = await processTask;

                        if (result.Success)
                        {
                            results[file] = result.Result;
                            batchResultsDict[file] = result.Result;
                            successCount++;
                            Console.WriteLine($"    ✓ Success: {fileName}");
                        }
                        else
                        {
                            batchErrorsDict[file] = result.Error ?? "Unknown error";
                            errorCount++;
                            Console.WriteLine($"    ✗ Error: {fileName} - {result.Error}");
                        }
                    }
                    else
                    {
                        // Timeout occurred
                        await cancellationTokenSource.CancelAsync();
                        
                        var timeoutError = $"Processing timeout after {timeoutMinutes} minutes - possible Tesseract hang or crash";
                        batchErrorsDict[file] = timeoutError;
                        errorCount++;
                        
                        Console.WriteLine($"    ✗ Timeout: {fileName}");

                        // Give extra time for cleanup after timeout
                        await Task.Delay(2000);
                    }
                }
                catch (Exception ex)
                {
                    // Catch any unexpected errors at file level
                    var criticalError = $"Critical error: {ex.Message}";
                    batchErrorsDict[file] = criticalError;
                    errorCount++;
                    
                    Console.WriteLine($"    ✗ Critical Error: {fileName} - {ex.Message}");
                }

                // Small delay between files to allow cleanup and GC
                await Task.Delay(500);

                // Save progress after every 3 files to minimize data loss
                if ((successCount + errorCount) % 3 == 0)
                {
                    try
                    {
                        if (batchResultsDict.Count > 0)
                        {
                            WriteResultsToCsv(directoryPath, batchResultsDict);
                            batchResultsDict.Clear();
                        }
                        if (batchErrorsDict.Count > 0)
                        {
                            WriteErrorsToCsv(directoryPath, batchErrorsDict);
                            batchErrorsDict.Clear();
                        }
                    }
                    catch (Exception csvEx)
                    {
                        Console.WriteLine($"Error saving intermediate results: {csvEx.Message}");
                    }
                }
            }

            // Save any remaining results from the batch
            try
            {
                if (batchResultsDict.Count > 0)
                {
                    WriteResultsToCsv(directoryPath, batchResultsDict);
                }

                if (batchErrorsDict.Count > 0)
                {
                    WriteErrorsToCsv(directoryPath, batchErrorsDict);
                }

                Console.WriteLine($"Batch {batchPosition}/{batches.Count} completed: {successCount} successful, {errorCount} errors. Saved to CSV.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving batch results: {ex.Message}");
            }
        }

        return results;
    }
    private void InitializeDefaultRules()
    {
        _ruleEngine.AddRule(new LicenceFileTypeRule());
        _ruleEngine.AddRule(new AddendumFileTypeRule());
    }
}