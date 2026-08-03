using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Helpers;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

public class NationalRiversWithoutSplitTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "NRA-Scanned";
    public string? Region { get; set; }
    public int Priority => 5;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "NationalRivers").Any() == true && content.Matches?
            .Where(m => m.LabelGroupName == $"{Region}SplitLabels").Any() == false;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        var secondaryTemplate = RuleSharedHelper.DetermineSecondaryTemplate(content);
        
        return new TemplateFinderResult
        {
            TemplateType = RuleName,
            Template = $"NRA{secondaryTemplate}"
        };
    }
}