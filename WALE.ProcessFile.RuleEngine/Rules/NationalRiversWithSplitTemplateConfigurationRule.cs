using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Helpers;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class NationalRiversWithSplitTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "NRA-Scanned (Potential Spilt Required)";
    public string? Region { get; set; }
    public int Priority => 2;

    public bool CanApply(MatchesResult content)
    {
        if (content.Matches == null) return false;

        var hasNationalRivers = content.Matches.Any(m => m.LabelGroupName == "NationalRivers");
        var hasSplitLabels = content.Matches.Any(m => m.LabelGroupName == $"{Region}SplitLabels");

        return hasNationalRivers && hasSplitLabels;
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