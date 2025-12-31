using WALE.ProcessFile.Models;
using WALE.ProcessFile.RuleEngine.Engine;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.RuleConfiguration;
using WALE.ProcessFile.RuleEngine.Rules;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Configuration;

namespace WALE.ProcessFile.RuleEngine.Services;

/// <summary>
/// Service for identifying file types based on content analysis using PDF text extraction
/// </summary>
public class TemplateTypeIdentifierService
{
    private readonly IRuleEngine<TemplateFinderResult> _ruleEngine;
    private readonly IPdfDataExtractorService? _pdfExtractorService;

    /// <summary>
    /// Initializes a new instance of FileTypeIdentifierService with PDF extractor service
    /// </summary>
    /// <param name="pdfExtractorService">PDF extractor service with OCR support</param>
    public TemplateTypeIdentifierService(IPdfDataExtractorService pdfExtractorService)
    {
        _ruleEngine = new RuleEngine<TemplateFinderResult>();
        _pdfExtractorService = pdfExtractorService ?? throw new ArgumentNullException(nameof(pdfExtractorService));
        InitializeDefaultRules();
    }
    
    public async Task<TemplateFinderResult?> IdentifyTemplateTypeAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"File not found: {filePath}");

        var content = await _pdfExtractorService?.GetMatchesAsync(
            filePath, 
            new LookupConfiguration(TemplateFinderRuleConfiguration.GetLabels(), new() {{"", ""}})
            , new List<string>(), 0)! ?? new MatchesResult();

        var result = _ruleEngine.Evaluate(content);
        if (result != null)
        {
            result.FileName = Path.GetFileName(filePath);
            result.NumberOfPages = content.NumberOfPages;
            return result; // Stop on first successful evaluation
        }

        return null; // No configuration matched
    }
    private void InitializeDefaultRules()
    {
        _ruleEngine.AddRule(new EADigitalTemplateConfigurationRule());
        _ruleEngine.AddRule(new EAScannedWithSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new EAScannedWithoutSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new NationalRiversWithSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new NationalRiversWithoutSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new YorkshireWaterTemplateConfigurationRule());
        _ruleEngine.AddRule(new YorkshireRiverTemplateConfigurationRule());
        _ruleEngine.AddRule(new NorthumbrianWaterTemplateConfigurationRule());
        _ruleEngine.AddRule(new NorthumbrianRiverTemplateConfigurationRule());
    }
}
