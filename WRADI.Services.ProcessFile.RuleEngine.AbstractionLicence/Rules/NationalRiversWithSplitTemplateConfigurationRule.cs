using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Helpers;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

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