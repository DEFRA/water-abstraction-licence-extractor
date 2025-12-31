using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.Services.Configuration;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class EAScannedWithoutSplitTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    private readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> _configuration;
    public string RuleName => $"EA-Scanned";
    public int Priority => 3;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "EALabel")?.Any() == true && content.Matches?
            .Where(m => m.LabelGroupName == "SplitLabels")?.Any() == false;
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
