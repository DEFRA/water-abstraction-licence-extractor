using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class EaDigitalTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "EA-Digital";
    public string? Region { get; set; }
    public int Priority => 1;

    public bool CanApply(MatchesResult content)
    {
        return content.ServicesUsed.Count == 1
               && content.ServicesUsed.First().Contains("pig", StringComparison.OrdinalIgnoreCase);
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