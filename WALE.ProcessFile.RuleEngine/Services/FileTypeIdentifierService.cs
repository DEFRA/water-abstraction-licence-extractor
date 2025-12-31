using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Engine;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Configuration;

namespace WALE.ProcessFile.RuleEngine.Services;

/// <summary>
/// Service for identifying file types based on content analysis using PDF text extraction
/// </summary>
public class FileTypeIdentifierService
{
    private readonly IRuleEngine<FileTypeResult> _ruleEngine;
    private readonly IPdfDataExtractorService? _pdfExtractorService;

    /// <summary>
    /// Initializes a new instance of FileTypeIdentifierService with PDF extractor service
    /// </summary>
    /// <param name="pdfExtractorService">PDF extractor service with OCR support</param>
    public FileTypeIdentifierService(IPdfDataExtractorService pdfExtractorService)
    {
        _ruleEngine = new RuleEngine<FileTypeResult>();
        _pdfExtractorService = pdfExtractorService ?? throw new ArgumentNullException(nameof(pdfExtractorService));
        InitializeDefaultRules();
    }

    /// <summary>
    /// Identifies the file type based on the content of a file using OCR when needed
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <returns>The file type identification result, or null if no type could be identified</returns>
    public async Task<FileTypeResult?> IdentifyFileTypeAsync(string filePath, LookupConfiguration configuration)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = await _pdfExtractorService?.GetMatchesAsync(
            filePath, configuration, new List<string>(), 0)! ?? new MatchesResult();
        return _ruleEngine.Evaluate(content);
    }

    /// <summary>
    /// Processes all files in a directory and identifies their types
    /// </summary>
    /// <param name="directoryPath">The directory path to process</param>
    /// <param name="searchPattern">File search pattern (default: "*.*")</param>
    /// <returns>A dictionary mapping file paths to their identification results</returns>
    public async Task<Dictionary<string, FileTypeResult?>> ProcessDirectoryAsync(string directoryPath, LookupConfiguration lookupConfiguration, string searchPattern = "*.*")
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var results = new Dictionary<string, FileTypeResult?>();
        var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly).Distinct();

        // Define terms to exclude
        var excludeTerms = new[] { "letter", "WR51", "determination" };

        // Filter out excluded files
        var filteredFiles = files
           // .Where(f => f.Contains("53113g0080__visit sheet 1998 7535765"))
            .Where(file =>
        {
            var fileName = Path.GetFileName(file).ToLowerInvariant();
            return !excludeTerms.Any(term => fileName.Contains(term.ToLowerInvariant()));
        }).ToList();

        // Process files in batches of 10
        const int batchSize = 10;
        var batches = filteredFiles
            .Select((file, index) => new { file, index })
            .GroupBy(x => x.index / batchSize)
            .Select(g => g.Select(x => x.file).ToList())
            .ToList();

        foreach (var batch in batches)
        {
            var batchTasks = batch.Select(async file =>
            {
                try
                {
                    var result = await IdentifyFileTypeAsync(file, configuration: lookupConfiguration);
                    return new KeyValuePair<string, FileTypeResult?>(file, result);
                }
                catch (Exception ex)
                {
                    // Log the error but continue processing other files
                    Console.WriteLine($"Error processing file {file}: {ex.Message}");
                    return new KeyValuePair<string, FileTypeResult?>(file, null);
                }
            });

            // Wait for all tasks in the current batch to complete
            var batchResults = await Task.WhenAll(batchTasks);

            // Add batch results to the main results dictionary
            foreach (var result in batchResults)
            {
                results[result.Key] = result.Value;
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
