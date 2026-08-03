using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

public class EaDigitalTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "EA-Digital";
    public string? Region { get; set; }
    public int Priority => 1;

    public bool CanApply(MatchesResult content)
    {
        return !content.ScannedFile;
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