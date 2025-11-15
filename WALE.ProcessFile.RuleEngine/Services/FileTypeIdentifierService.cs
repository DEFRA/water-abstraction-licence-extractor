using WALE.ProcessFile.RuleEngine.Engine;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Configuration;

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

        var content = await ExtractPdfContentAsync(filePath, configuration);
        return IdentifyFileType(content);
    }

    /// <summary>
    /// Identifies the file type based on text content
    /// </summary>
    /// <param name="content">The text content to analyze</param>
    /// <returns>The file type identification result, or null if no type could be identified</returns>
    public FileTypeResult? IdentifyFileType(string content)
    {
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
        var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            try
            {
                var result = await IdentifyFileTypeAsync(file, configuration: lookupConfiguration);
                results[file] = result;
            }
            catch (Exception ex)
            {
                // Log the error but continue processing other files
                Console.WriteLine($"Error processing file {file}: {ex.Message}");
                results[file] = null;
            }
        }

        return results;
    }

    /// <summary>
    /// Extracts text content from PDF files using GetMatchesAsync like other tool classes
    /// </summary>
    /// <param name="filePath">The path to the PDF file</param>
    /// <param name="configuration">The lookup configuration</param>
    /// <returns>The extracted text content</returns>
    private async Task<string> ExtractPdfContentAsync(string filePath, LookupConfiguration configuration)
    {
        if (_pdfExtractorService == null)
        {
            Console.WriteLine("PDF extractor service not available, cannot process PDF files");
            return string.Empty;
        }

        try
        {
            // Use GetMatchesAsync to extract content (same pattern as other Tools classes)
            var result = await _pdfExtractorService.GetMatchesAsync(filePath, configuration, new List<string>());

            // Extract all text from matches and sub-results
            var allText = new List<string>();

            if (result.Matches != null)
            {
                foreach (var match in result.Matches)
                {
                    // Extract text from the main match
                    if (match.Text != null)
                    {
                        foreach (var line in match.Text)
                        {
                            if (!string.IsNullOrEmpty(line.Text))
                            {
                                allText.Add(line.Text);
                            }
                        }
                    }

                    // Extract text from sub-results recursively
                    ExtractTextFromSubResults(match.SubResults, allText);
                }
            }

            return string.Join(" ", allText);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting PDF content from {filePath}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Recursively extracts text from sub-results
    /// </summary>
    /// <param name="subResults">The sub-results to extract text from</param>
    /// <param name="allText">The list to add extracted text to</param>
    private void ExtractTextFromSubResults(IReadOnlyList<LabelGroupResult> subResults, List<string> allText)
    {
        foreach (var subResult in subResults)
        {
            // Extract text from this sub-result
            if (subResult.Text != null)
            {
                foreach (var line in subResult.Text)
                {
                    if (!string.IsNullOrEmpty(line.Text))
                    {
                        allText.Add(line.Text);
                    }
                }
            }

            // Recursively process nested sub-results
            if (subResult.SubResults.Any())
            {
                ExtractTextFromSubResults(subResult.SubResults, allText);
            }
        }
    }

    private void InitializeDefaultRules()
    {
        _ruleEngine.AddRule(new LicenceFileTypeRule());
        _ruleEngine.AddRule(new AddendumFileTypeRule());
    }
}
