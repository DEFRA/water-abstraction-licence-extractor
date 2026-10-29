using WALE.ProcessFile.Core.Models;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Helpers;

public static class RuleSharedHelper
{
    /// Ordered most-specific first: the patterns overlap - "Northumbria and Yorkshire Region"
    /// satisfies NENRAModern1, NENRAModern2 and NRAOld alike - so whichever is tested first wins.
    /// LabelGroupName must match what TemplateHeaderLabels registers; RegionRegex names which
    /// groups share a pattern, which is what DetermineSecondaryTemplate's tie-break keys on.
    private static readonly (string LabelGroupName, string TemplateName, string RegionRegex)[]
        TemplateMapping =
    [
        ("NENRAModern1", "Modern 1", "NorthumbriaYorkshireRegion"),
        ("NENRAModern2", "Modern 2", "YorkshireRegion"),
        ("NRAOld", "Old", "NorthumbriaRegion"),
        ("AnglianNRAModern1", "Anglian Modern 1", "AnglianRegion"),
        ("AnglianNRAModern2", "Anglian Modern 2", "AnglianRegion"),
        ("NWNRAModern1", "NW Modern 1", "NorthWestRegion"),
        ("NWNRAOld", "Old", "NationalRiversAuthority")
    ];

    public static string DetermineSecondaryTemplate(MatchesResult? matches)
    {
        if (matches == null)
        {
            return string.Empty;
        }

        var candidates = TemplateMapping
            .Where(mapping => HasLabel(matches, mapping.LabelGroupName, "Region"))
            .ToList();

        if (candidates.Count == 0)
        {
            return string.Empty;
        }

        // Requiring the Licence label of every group instead would lose the ones whose licence
        // line OCRs badly - a real document matched "Licence Serlel No" and no Region at all.
        var tied = candidates
            .Where(mapping => mapping.RegionRegex == candidates[0].RegionRegex)
            .ToList();

        if (tied.Count == 1)
        {
            return candidates[0].TemplateName;
        }

        var onLicenceLabel = tied
            .Where(mapping => HasLabel(matches, mapping.LabelGroupName, "Licence"))
            .ToList();

        return onLicenceLabel.Count == 1
            ? onLicenceLabel[0].TemplateName
            : candidates[0].TemplateName;
    }

    /// MatchedLabelName, not MatchedLabel.Name: the latter is [JsonIgnore] and null on a
    /// MatchesResult read back from the database, leaving every secondary template unresolvable.
    private static bool HasLabel(MatchesResult matches, string labelGroupName, string labelName)
    {
        return matches.Matches?
            .Any(m => m.LabelGroupName == labelGroupName
                && m.MatchedLabelName == labelName) == true;
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