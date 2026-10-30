using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

public class EaScannedWithSplitTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "EA-Scanned (Potential Split Required)";
    public string? Region { get; set; }
    public int Priority => 2;

    /// Reports "Scanned" and outranks EaDigital, so it must not claim a native document. Guarded
    /// here rather than by raising EaDigital above this rule, which would lose the split signal.
    public bool CanApply(MatchesResult content)
    {
        return content.ScannedFile && content.Matches?
            .Where(m => m.LabelGroupName == "EALabel").Any() == true && content.Matches?
            .Where(m => m.LabelGroupName == $"{Region}SplitLabels").Any() == true;
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