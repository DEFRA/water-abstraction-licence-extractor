using WALE.ProcessFile.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.Services.Configuration;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class YorkshireRiverTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    private readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> _configuration;
    public string RuleName => $"Yorkshire River Authority";
    public int Priority => 7;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "NationalRivers")?.Any() == false && content.Matches?
            .Where(m => m.LabelGroupName == "YorkshireRiver")?.Any() == true;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        return new TemplateFinderResult
        {
            TemplateType = RuleName,
            Template = "NRA"
        };
    }
}
