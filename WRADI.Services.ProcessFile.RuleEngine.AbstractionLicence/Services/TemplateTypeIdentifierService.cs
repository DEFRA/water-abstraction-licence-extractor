using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Engine;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Services;

/// <summary>
/// Service for identifying file types based on content analysis using PDF text extraction
/// </summary>
public class TemplateTypeIdentifierService
{
    private readonly IRuleEngine<TemplateFinderResult> _ruleEngine;

    /// <summary>
    /// Initializes a new instance of FileTypeIdentifierService with PDF extractor service
    /// </summary>
    /// <param name="regionCode2Letters">...</param>
    public TemplateTypeIdentifierService(string regionCode2Letters)
    {
        _ruleEngine = new RuleEngine<TemplateFinderResult>();
        InitializeDefaultRules(regionCode2Letters);
    }
    
    public TemplateFinderResult? IdentifyTemplateType(MatchesResult content)
    {
        return _ruleEngine.Evaluate(content);
    }
    
    private void InitializeDefaultRules(string region)
    {
        _ruleEngine.AddRule(new EaDigitalTemplateConfigurationRule());
        _ruleEngine.AddRule(new EaScannedWithSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new EaScannedWithoutSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new NationalRiversWithSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new NationalRiversWithoutSplitTemplateConfigurationRule());
        _ruleEngine.AddRule(new DivisionalTemplateConfigurationRule());
        
        _ruleEngine.SetRegion(region);
    }
}