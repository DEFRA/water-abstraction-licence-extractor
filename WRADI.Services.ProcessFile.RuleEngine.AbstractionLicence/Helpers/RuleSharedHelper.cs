using WALE.ProcessFile.Core.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Helpers;

public static class RuleSharedHelper
{
    /// The patterns overlap - "Northumbria and Yorkshire Region" satisfies NENRAModern1,
    /// NENRAModern2 and NRAOld alike - so the first tested wins. Ordered rather than a Dictionary
    /// because enumeration order must not be what decides it. Keys are the group names
    /// TemplateHeaderLabels registers.
    ///
    /// Known gap: AnglianNRAModern1 and AnglianNRAModern2 share one Region regex, so Anglian always
    /// resolves to Modern 1. Telling them apart needs their Licence label, which is not consulted.
    private static readonly (string LabelGroupName, string TemplateName)[] TemplateMapping =
    [
        ("NENRAModern1", "Modern 1"),
        ("NENRAModern2", "Modern 2"),
        ("NRAOld", "Old"),
        ("AnglianNRAModern1", "Anglian Modern 1"),
        ("AnglianNRAModern2", "Anglian Modern 2"),
        ("NWNRAModern1", "NW Modern 1"),
        ("NWNRAOld", "Old")
    ];

    public static string DetermineSecondaryTemplate(MatchesResult? matches)
    {
        if (matches == null)
        {
            return string.Empty;
        }

        foreach (var (labelGroupName, templateName) in TemplateMapping)
        {
            if (HasRequiredLabels(matches, labelGroupName))
                return templateName;
        }

        return string.Empty;
    }

    /// MatchedLabelName, not MatchedLabel.Name: the latter is [JsonIgnore] and null on a
    /// MatchesResult read back from the database, leaving every secondary template unresolvable.
    ///
    /// The group's other label, "Licence", is deliberately not required: it also matches EA forms,
    /// so accepting it alone would classify EA documents as NRA.
    private static bool HasRequiredLabels(MatchesResult matches, string labelGroupName)
    {
        var groupMatches = matches.Matches?
            .Where(m => m.LabelGroupName == labelGroupName)
            .ToList() ?? [];

        return groupMatches.Any(c => c.MatchedLabelName == "Region");
    }
    
    public static string? ExtractLicenceNumber(MatchesResult matchesResult)
    {
        var licenceNumberMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "LicenceNumber");

        if (!(licenceNumberMatch?.Text?.Count > 0))
        {
            return null;
        }
        
        var lines = licenceNumberMatch.Text
            .Select(line => line.Text);
            
        return string.Join(" ", lines).Trim();
    }

    public static string? ExtractDateOfIssue(MatchesResult matchesResult)
    {
        var dateOfIssueMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "DateOfIssue");

        if (!(dateOfIssueMatch?.Text?.Count > 0))
        {
            return null;
        }

        var lines = dateOfIssueMatch.Text
            .Select(line => line.Text);
            
        return string.Join(" ", lines).Trim();
    }
    
    public static string? ExtractIssuerName(MatchesResult matchesResult)
    {
        var dateOfIssueMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "Issuer");

        if (dateOfIssueMatch?.Text != null && dateOfIssueMatch.Text.Count > 0)
        {
            return string.Join(" ", dateOfIssueMatch.Text
                    .SelectMany(line => line.Text)
                    .Select(element => element))
                .Trim()
                .Replace(" ", "");
        }

        return null;
    }
    public static string? ExtractVariationName(MatchesResult matchesResult)
    {
        var dateOfIssueMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "Variation");

        if (dateOfIssueMatch?.Text != null && dateOfIssueMatch.Text.Count > 0)
        {
            return string.Join(" ", dateOfIssueMatch.Text
                    .SelectMany(line => line.Text)
                    .Select(element => element))
                .Trim()
                .Replace(" ", "");
        }

        return null;
    }

    public static string? ExtractPermitNumberFromFilename(string filename)
    {
        if (string.IsNullOrEmpty(filename))
            return null;

        // Remove file extension first
        var nameWithoutExtension = Path.GetFileNameWithoutExtension(filename);

        // Find first underscore and extract everything before it
        var underscoreIndex = nameWithoutExtension.IndexOf('_');

        if (underscoreIndex > 0)
        {
            return nameWithoutExtension.Substring(0, underscoreIndex).Replace(" ", "");
        }

        // If no underscore found, return the whole filename without extension
        return nameWithoutExtension.Replace(" ", "");
    }
}