using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;

namespace WALE.ProcessFile.RuleEngine.Rules;

public class DivisionalTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    private readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> _configuration;
    public string RuleName => $"Divisional";
    public string Region { get; set; }
    public int Priority => 9;

    public bool CanApply(MatchesResult content)
    {
        return content.Matches?
            .Where(m => m.LabelGroupName == "NationalRivers")?.Any() == false && content.Matches?
            .Where(m => m.LabelGroupName == $"{Region}SplitLabels")?.Any() == true;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        var matchedLabel = content.Matches?
            .Where(m => m.LabelGroupName == $"{Region}SplitLabels")
            ?.Select(c => c.MatchedLabel)?.FirstOrDefault()?.Name;
        return new TemplateFinderResult
        {
            TemplateType = matchedLabel,
            Template = matchedLabel
        };
    }
}
