using WALE.ProcessFile.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.Services.Configuration;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class YorkshireWaterTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    private readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> _configuration;
    public string RuleName => $"Yorkshire Water Authority";
    public int Priority => 6;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "NationalRivers")?.Any() == false && content.Matches?
            .Where(m => m.LabelGroupName == "YorkshireWater")?.Any() == true;
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
