using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Interfaces;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;

/// Last resort, for documents whose letterhead never matched. The Issuer group is not positionally
/// constrained like the header groups and will match an authority named in body text, hence lowest
/// priority.
///
/// Known gap: the patterns cover the eight issuer strings in the estate, not the 20 GetIssuerLabels
/// configures - four water-company and board names still classify as null.
public partial class IssuerDerivedTemplateConfigurationRule : IRule<TemplateFinderResult>
{
    public string RuleName => "Issuer-Derived";

    public string? Region { get; set; }

    public int Priority => 0;

    public bool CanApply(MatchesResult content)
    {
        return Classify(content) != null;
    }

    public TemplateFinderResult Apply(MatchesResult content)
    {
        // Throws if called without CanApply having returned true first, which is the engine's
        // contract but not this method's signature.
        var (templateType, template) = Classify(content)!.Value;

        return new TemplateFinderResult
        {
            TemplateType = templateType,
            Template = template
        };
    }

    private static (string TemplateType, string Template)? Classify(MatchesResult content)
    {
        var raw = IssuerText(content);

        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        // Collapsed before the tests below, not after: "Yorkshire  Water   Authority" does not
        // contain "Water Authority", so matching raw OCR text would miss the document entirely.
        var issuer = NormaliseWhitespaceRegex().Replace(raw, " ").Trim();

        if (issuer.Contains("Environment Agency", StringComparison.OrdinalIgnoreCase))
        {
            return ("EA-Scanned", "EA");
        }

        // Before the generic "River Authority" test below, which would otherwise claim it.
        if (issuer.Contains("National Rivers Authority", StringComparison.OrdinalIgnoreCase))
        {
            return ("NRA-Scanned", "NRA");
        }

        if (issuer.Contains("Water Authority", StringComparison.OrdinalIgnoreCase)
            || issuer.Contains("River Authority", StringComparison.OrdinalIgnoreCase))
        {
            // The authority name, not a class name, because DivisionalTemplateConfigurationRule
            // puts the authority in TemplateType and two vocabularies would split any grouping.
            var authority = issuer.ToUpperInvariant();

            return (authority, authority);
        }

        return null;
    }

    /// Not RuleSharedHelper.ExtractIssuerName: that rebuilds the text a character at a time and
    /// strips the spaces these patterns match on.
    private static string? IssuerText(MatchesResult content)
    {
        var issuerMatch = content.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "Issuer");

        return issuerMatch?.Text == null
            ? null
            : string.Join(" ", issuerMatch.Text.Select(line => line.Text));
    }

    [GeneratedRegex(@"\s+")]
    private static partial Regex NormaliseWhitespaceRegex();
}
