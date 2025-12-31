using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class EAScannedWithSplitTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    private readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> _configuration;
    public string RuleName => $"EA-Scanned (Potential Spilt Required)";
    public int Priority => 2;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "EALabel")?.Any() == true && content.Matches?
            .Where(m => m.LabelGroupName == "SplitLabels")?.Any() == true;
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
