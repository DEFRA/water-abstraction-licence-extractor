using WALE.ProcessFile.RuleEngine.Engine;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.RuleConfiguration;
using WALE.ProcessFile.RuleEngine.Rules;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Services;

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
    /// <param name="region">TODO</param>
    public TemplateTypeIdentifierService(IPdfDataExtractorService pdfExtractorService, string region)
    {
        _ruleEngine = new RuleEngine<TemplateFinderResult>();
        _pdfExtractorService = pdfExtractorService
            ?? throw new ArgumentNullException(nameof(pdfExtractorService));
        
        InitializeDefaultRules(region);
    }
    
    public async Task<TemplateFinderResult?> IdentifyTemplateTypeAsync(string fileName)
    {
        if (!File.Exists(fileName))
        {
            throw new FileNotFoundException($"File not found: {fileName}");
        }

        if (_pdfExtractorService == null)
        {
            throw new NullReferenceException("_pdfExtractorService");
        }
        
        const int regionCode = 3; // NE
        const int processRunId = -1;
        
        var lookupConfig = new LookupConfiguration(
            TemplateFinderRuleConfiguration.GetLabels(),
            [], // TODO
            [], // TODO
            [], // TODO
            new LocalFileService("TODO"),
            new FileSystemCacheService("TODO"),
            regionCode);
        
        var filenameParts = fileName.Split("__");
        var fileId = filenameParts.Length >= 3 ? Guid.Parse(filenameParts[1]) : Guid.Empty;
        
        var content = await _pdfExtractorService!.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = fileId },
            lookupConfig,
            [],
            processRunId)!;

        var result = _ruleEngine.Evaluate(content);

        if (result == null) 
        {
            return null; // No configuration matched
        }
        
        result.FileName = Path.GetFileName(fileName);
        result.NumberOfPages = content.NumberOfPages;
            
        return result; // Stop on first successful evaluation

    }
    private void InitializeDefaultRules(string region)
    {
        _ruleEngine.AddRule(new EADigitalTemplateConfigurationRule());
        _ruleEngine.AddRule(new EAScannedWithSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new EAScannedWithoutSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new NationalRiversWithSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new NationalRiversWithoutSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new DivisionalTemplateConfigurationRule());
        
        _ruleEngine.SetRegion(region);
    }
}