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
    /// Initializes a new instance of FileTypeIdentifierService with default rules
    /// </summary>
    public FileTypeIdentifierService()
    {
        _ruleEngine = new RuleEngine<FileTypeResult>();
        InitializeDefaultRules();
    }

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
    /// Initializes a new instance of FileTypeIdentifierService with all custom components
    /// </summary>
    /// <param name="ruleEngine">The rule engine to use</param>
    /// <param name="pdfExtractorService">PDF extractor service with OCR support</param>
    public FileTypeIdentifierService(IRuleEngine<FileTypeResult> ruleEngine, IPdfDataExtractorService pdfExtractorService)
    {
        _ruleEngine = ruleEngine ?? throw new ArgumentNullException(nameof(ruleEngine));
        _pdfExtractorService = pdfExtractorService ?? throw new ArgumentNullException(nameof(pdfExtractorService));
        InitializeDefaultRules();
    }

    /// <summary>
    /// Identifies the file type based on the content of a file using OCR when needed
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <returns>The file type identification result, or null if no type could be identified</returns>
    public async Task<FileTypeResult?> IdentifyFileTypeAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = await ExtractContentAsync(filePath);
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
    /// Gets all possible file type identifications for the given content
    /// </summary>
    /// <param name="content">The text content to analyze</param>
    /// <returns>All matching file type identification results</returns>
    public IEnumerable<FileTypeResult> IdentifyAllFileTypes(string content)
    {
        return _ruleEngine.EvaluateAll(content);
    }

    /// <summary>
    /// Processes all files in a directory and identifies their types
    /// </summary>
    /// <param name="directoryPath">The directory path to process</param>
    /// <param name="searchPattern">File search pattern (default: "*.*")</param>
    /// <returns>A dictionary mapping file paths to their identification results</returns>
    public async Task<Dictionary<string, FileTypeResult?>> ProcessDirectoryAsync(string directoryPath, string searchPattern = "*.*")
    {
        if (!Directory.Exists(directoryPath))
            throw new DirectoryNotFoundException($"Directory not found: {directoryPath}");

        var results = new Dictionary<string, FileTypeResult?>();
        var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);

        foreach (var file in files)
        {
            try
            {
                var result = await IdentifyFileTypeAsync(file);
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
    /// Adds a custom rule to the rule engine
    /// </summary>
    /// <param name="rule">The rule to add</param>
    public void AddRule(IRule<FileTypeResult> rule)
    {
        _ruleEngine.AddRule(rule);
    }

    /// <summary>
    /// Removes a rule from the rule engine
    /// </summary>
    /// <param name="ruleName">The name of the rule to remove</param>
    /// <returns>True if the rule was removed, false if it wasn't found</returns>
    public bool RemoveRule(string ruleName)
    {
        return _ruleEngine.RemoveRule(ruleName);
    }

    /// <summary>
    /// Gets all currently registered rules
    /// </summary>
    /// <returns>A collection of all registered rules</returns>
    public IEnumerable<IRule<FileTypeResult>> GetRules()
    {
        return _ruleEngine.GetRules();
    }

    /// <summary>
    /// Extracts text content from a file using appropriate method based on file type
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <returns>The extracted text content</returns>
    private async Task<string> ExtractContentAsync(string filePath)
    {
        try
        {
            return await ExtractPdfContentAsync(filePath);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error extracting content from {filePath}: {ex.Message}");
            return string.Empty;
        }
    }

    /// <summary>
    /// Extracts text content from PDF files using PdfDataExtractorService like PdfContentReaderExtract
    /// </summary>
    /// <param name="filePath">The path to the PDF file</param>
    /// <returns>The extracted text content</returns>
    private async Task<string> ExtractPdfContentAsync(string filePath)
    {
        if (_pdfExtractorService == null)
        {
            Console.WriteLine("PDF extractor service not available, cannot process PDF files");
            return string.Empty;
        }

        try
        {
            // Create a minimal configuration (same pattern as used in other Tools classes)
            var configuration = new LookupConfiguration(
                new List<(string LabelGroupName, List<LabelToMatch> Labels)>(),
                new Dictionary<string, string>(),
                Path.GetTempPath(),
                Path.GetTempPath());

            var result = await _pdfExtractorService.GetPagesAsync(filePath, configuration);

            // Extract all text from all pages (same pattern as other Tools classes)
            var allText = new List<string>();

            if (result.Pages != null)
            {
                foreach (var page in result.Pages)
                {
                    if (page.Text != null)
                    {
                        allText.Add(page.Text);
                    }
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

    private void InitializeDefaultRules()
    {
        _ruleEngine.AddRule(new LicenceFileTypeRule());
        _ruleEngine.AddRule(new AddendumFileTypeRule());
    }
}
