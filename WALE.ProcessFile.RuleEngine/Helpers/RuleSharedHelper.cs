using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.RuleEngine.Helpers;

public static class RuleSharedHelper
{
    private static readonly Dictionary<string, string> TemplateMapping = new()
    {
        { "NRAModern1", "Modern 1" },
        { "NRAModern2", "Modern 2" },
        { "AnglianNRAModern1", "Anglian Modern 1" },
        { "AnglianNRAModern2", "Anglian Modern 2" },
        { "NRAOld", "Old" },
        { "NWNRAModern1", "NW Modern 1" },
        { "NWNRAOld", "Old" }
    };
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

    private static bool HasRequiredLabels(MatchesResult matches, string labelGroupName)
    {
        var groupMatches = matches.Matches?.Where(m => m.LabelGroupName == labelGroupName).ToList();

        return groupMatches!.Any(c => c.MatchedLabel?.Name == "Region");// &&
               //groupMatches.Any(c => c.MatchedLabel?.Name == "Licence");
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