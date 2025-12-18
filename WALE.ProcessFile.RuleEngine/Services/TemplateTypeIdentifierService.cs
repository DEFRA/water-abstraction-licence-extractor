using WALE.ProcessFile.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.RuleConfiguration;
using WALE.ProcessFile.RuleEngine.Rules;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Configuration;

namespace WALE.ProcessFile.RuleEngine.Services;

/// <summary>
/// Service for identifying template types based on content analysis using PDF text extraction
/// </summary>
public class TemplateTypeIdentifierService
{
    private readonly IRuleEngine<TemplateFinderResult> _ruleEngine;
    private readonly IPdfDataExtractorService? _pdfExtractorService;

    /// <summary>
    /// Initializes a new instance of TemplateTypeIdentifierService with PDF extractor service
    /// </summary>
    /// <param name="pdfExtractorService">PDF extractor service with OCR support</param>
    public TemplateTypeIdentifierService(IPdfDataExtractorService pdfExtractorService)
    {
        _ruleEngine = new Engine.RuleEngine<TemplateFinderResult>();
        _pdfExtractorService = pdfExtractorService ?? throw new ArgumentNullException(nameof(pdfExtractorService));
    }

    /// <summary>
    /// Gets the list of all available template configurations with their user-friendly names
    /// </summary>
    /// <returns>List of tuples containing configuration factory functions and their user-friendly names</returns>
    private List<(Func<LookupConfiguration> ConfigurationFactory, string TemplateName, int)> GetTemplateConfigurations()
    {
        return new List<(Func<LookupConfiguration>, string, int)>
        {
            (() => new LookupConfiguration(EADigitalTemplateOneConfiguration.GetLabels(), new() {{"", ""}}), "EA Digital Template 1", 8),
            (() => new LookupConfiguration(EAScannedTemplateOneConfiguration.GetLabels(), new() {{"", ""}}), "EA Scanned Template 1", 4),
            (() => new LookupConfiguration(EAScannedTemplateTwoConfiguration.GetLabels(), new() {{"", ""}}), "EA Scanned Template 2", 4),
            (() => new LookupConfiguration(EAScannedTemplateThreeConfiguration.GetLabels(), new() {{"", ""}}), "EA Scanned Template 3", 4),
            (() => new LookupConfiguration(EAScannedTemplateFourConfiguration.GetLabels(), new() {{"", ""}}), "EA Scanned Template 4", 9),
            (() => new LookupConfiguration(NationalRiverTemplateOneConfiguration.GetLabels(), new() {{"", ""}}), "National River Template 1", 4),
            (() => new LookupConfiguration(NationalRiverTemplateTwoConfiguration.GetLabels(), new() {{"", ""}}), "National River Template 2", 4),
            (() => new LookupConfiguration(NorthumbrianRiverTemplateOneConfiguration.GetLabels(), new() {{"", ""}}), "Northumbrian River Template 1", 4),
            (() => new LookupConfiguration(NorthumbrianWaterTemplateOneConfiguration.GetLabels(), new() {{"", ""}}), "Northumbrian Water Template 1", 5),
            (() => new LookupConfiguration(NorthumbrianWaterTemplateTwoConfiguration.GetLabels(), new() {{"", ""}}), "Northumbrian Water Template 2 ", 4),
            (() => new LookupConfiguration(YorkshireRiverTemplateOneConfiguration.GetLabels(), new() {{"", ""}}), "Yorkshire River Template 1", 4),
            (() => new LookupConfiguration(YorkshireWaterTemplateOneConfiguration.GetLabels(), new() {{"", ""}}), "Yorkshire Water Template 1", 4),
            (() => new LookupConfiguration(YorkshireWaterTemplateTwoConfiguration.GetLabels(), new() {{"", ""}}), "Yorkshire Water Template 2", 4),
            (() => new LookupConfiguration(YorkshireWaterTemplateThreeConfiguration.GetLabels(), new() {{"", ""}}), "Yorkshire Water Template 3", 4)
        };
    }

    /// <summary>
    /// Identifies the template type based on the content of a file using OCR when needed
    /// </summary>
    /// <param name="filePath">The path to the file</param>
    /// <param name="configurations">Optional list of configurations to try. If null, uses all available configurations.</param>
    /// <returns>The template identification result, or null if no template could be identified</returns>
    public async Task<TemplateFinderResult?> IdentifyTemplateTypeAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var configurations = GetTemplateConfigurations();

        foreach (var (configurationFactory, templateName, pageCount) in configurations)
        {
            var configuration = configurationFactory();
            var content = await _pdfExtractorService?.GetMatchesAsync(
                filePath, configuration, new List<string>(), 0)! ?? new MatchesResult();

            // Create a temporary rule engine with the specific configuration
            var tempRuleEngine = new Engine.RuleEngine<TemplateFinderResult>();
            tempRuleEngine.AddRule(new TemplateConfigurationRule(configuration, templateName, pageCount));

            var result = tempRuleEngine.Evaluate(content);
            if (result != null)
            {
                result.FileName = Path.GetFileName(filePath);
                result.NumberOfPages = content.NumberOfPages;
                return result; // Stop on first successful evaluation
            }
        }

        return null; // No configuration matched
    }
}
