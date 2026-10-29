using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Helpers;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

/// For NRA-era forms the two NationalRivers rules miss: no "NationalRivers" match, but a per-region
/// group resolved a secondary template. Without this, those seven groups are matched and consulted
/// by nothing.
public class NationalRiversSecondaryTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    /// RuleEngine.AddRule drops any rule already registered under this name, so it must not be
    /// "NRA-Scanned" - that would unregister NationalRiversWithoutSplit.
    public string RuleName => "NRA-Scanned (Secondary Template)";

    private const string ReportedTemplateType = "NRA-Scanned";

    public string? Region { get; set; }

    /// This outranks the priority-2 "WithSplit" rules, so the exclusions in CanApply - not the
    /// priority - are what stop it taking their documents. Divisional does not cover that case:
    /// it requires NationalRivers to be absent.
    public int Priority => 4;

    public bool CanApply(MatchesResult content)
    {
        if (!(content.Matches?.Count > 0) || !content.ScannedFile)
        {
            return false;
        }

        // The per-region "Region" regexes are loose enough to hit body text, so the ScannedFile
        // guard above carries the weight. Every NRA-era licence in the estate is scanned, 65 of 65.
        var claimedByAnotherRule =
            Matched(content, "NationalRivers")
            || Matched(content, $"{Region}SplitLabels");

        return !claimedByAnotherRule
            && !string.IsNullOrEmpty(RuleSharedHelper.DetermineSecondaryTemplate(content));
    }

    private static bool Matched(MatchesResult content, string labelGroupName)
    {
        return content.Matches?.Any(m => m.LabelGroupName == labelGroupName) == true;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        return new TemplateFinderResult
        {
            TemplateType = ReportedTemplateType,
            Template = $"NRA{RuleSharedHelper.DetermineSecondaryTemplate(content)}"
        };
    }
}
