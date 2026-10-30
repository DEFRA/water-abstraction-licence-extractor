using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

public class DivisionalTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "Divisional";
    public string? Region { get; set; }
    public int Priority => 9;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "NationalRivers").Any() == false && content.Matches?
            .Where(m => m.LabelGroupName == $"{Region}SplitLabels").Any() == true;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        var matchedLabel = content.Matches?
            .Where(m => m.LabelGroupName == $"{Region}SplitLabels")
            .Select(c => c.MatchedLabel)?.FirstOrDefault()?.Name;
        
        return new TemplateFinderResult
        {
            TemplateType = matchedLabel,
            Template = matchedLabel
        };
    }
}