using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class EaScannedWithoutSplitTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "EA-Scanned";
    public string? Region { get; set; }
    public int Priority => 3;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "EALabel").Any() == true && content.Matches?
            .Where(m => m.LabelGroupName == $"{Region}SplitLabels").Any() == false;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        return new TemplateFinderResult
        {
            TemplateType = RuleName,
            Template = "EA"
        };
    }
}