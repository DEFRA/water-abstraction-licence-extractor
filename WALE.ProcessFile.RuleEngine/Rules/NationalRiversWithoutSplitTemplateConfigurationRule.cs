using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Helpers;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class NationalRiversWithoutSplitTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    private readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> _configuration;
    public string RuleName => $"NRA-Scanned";
    public int Priority => 5;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "NationalRivers")?.Any() == true && content.Matches?
            .Where(m => m.LabelGroupName == "SplitLabels")?.Any() == false;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        var secondaryTemplate = RuleSharedHelper.DetermineSecondaryTemplate(content);
        return new TemplateFinderResult
        {
            TemplateType = RuleName,
            Template = "NRA" + secondaryTemplate
        };
    }
}
