using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.RuleEngine.Helpers;

public static class RuleSharedHelper
{

    private static readonly Dictionary<string, string> TemplateMapping = new()
    {
        { "NRAModern1", "Modern 1" },
        { "NRAModern2", "Modern 2" },
        { "NRAOld", "Old" }
    };
    public static string DetermineSecondaryTemplate(MatchesResult matches)
    {
        if (matches == null) return string.Empty;

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

        return groupMatches.Any(c => c.MatchedLabel?.Name == "Region");// &&
               //groupMatches.Any(c => c.MatchedLabel?.Name == "Licence");
    }
    
    public static string? DateFormatConsistent(string? input)
    {
        if (input == null)
        {
            return null;
        }
        
        ReplaceIfContains(input, " ", string.Empty, out input);
        ReplaceIfContains(input, "first", "1", out input);
        ReplaceIfContains(input, "second", "2", out input);
        ReplaceIfContains(input, "third", "3", out input);
        ReplaceIfContains(input, "fourth", "4", out input);
        ReplaceIfContains(input, "fifth", "5", out input);
        ReplaceIfContains(input, "sixth", "6", out input);
        ReplaceIfContains(input, "seventh", "7", out input);
        ReplaceIfContains(input, "eighth", "8", out input);
        ReplaceIfContains(input, "ninth", "9", out input);
        ReplaceIfContains(input, "tenth", "10", out input);
        ReplaceIfContains(input, "eleventh", "11", out input);
        ReplaceIfContains(input, "twelfth", "12", out input);
        ReplaceIfContains(input, "thirteenth", "13", out input);
        ReplaceIfContains(input, "fourteenth", "14", out input);
        ReplaceIfContains(input, "fifteenth", "15", out input);
        ReplaceIfContains(input, "sixteenth", "16", out input);
        ReplaceIfContains(input, "seventeenth", "17", out input);
        ReplaceIfContains(input, "eighteenth", "18", out input);
        ReplaceIfContains(input, "nineteenth", "19", out input);
        ReplaceIfContains(input, "twentieth", "20", out input);
        ReplaceIfContains(input, "twenty-first", "21", out input);
        ReplaceIfContains(input, "twenty-second", "22", out input);
        ReplaceIfContains(input, "twenty-third", "23", out input);
        ReplaceIfContains(input, "twenty-fourth", "24", out input);
        ReplaceIfContains(input, "twenty-fifth", "25", out input);
        ReplaceIfContains(input, "twenty-sixth", "26", out input);
        ReplaceIfContains(input, "twenty-seventh", "27", out input);
        ReplaceIfContains(input, "twenty-eighth", "28", out input);
        ReplaceIfContains(input, "twenty-ninth", "29", out input);
        ReplaceIfContains(input, "thirtieth", "30", out input);
        ReplaceIfContains(input, "thirty-first", "31", out input);
        ReplaceIfContains(input, "August", "Aug", out input);
        ReplaceIfContains(input, "DAYOF", string.Empty, out input);
        ReplaceIfContains(input, "st", string.Empty, out input);
        ReplaceIfContains(input, "nd", string.Empty, out input);
        ReplaceIfContains(input, "rd", string.Empty, out input);
        ReplaceIfContains(input, "IEH", string.Empty, out input); // misreading of TH
        ReplaceIfContains(input, "th", string.Empty, out input);
        
        ReplaceIfContains(input, "NAY", "MAY", out input); // misreading of TH - TODO should use autocorrect
        
        ReplaceIfContains(input, "196g", "1966", out input); // TODO this should be more generic (regex)
        ReplaceIfContains(input, "1575", "1975", out input); // TODO this should be more generic (regex)

        return input;
    }
    
    private static void ReplaceIfContains(string input, string match, string replaceWith, out string output)
    {
        output = input;

        if (!input.Contains(match, StringComparison.InvariantCultureIgnoreCase))
        {
            return;
        }
        
        output = input.Replace(match, replaceWith, StringComparison.InvariantCultureIgnoreCase);
    }
    
    public static string? ExtractLicenceNumber(MatchesResult matchesResult)
    {
        var licenceNumberMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "LicenceNumber");

        if (licenceNumberMatch?.Text != null && licenceNumberMatch.Text.Count > 0)
        {
            return string.Join(" ", licenceNumberMatch.Text
                    .SelectMany(line => line.Text)
                    .Select(element => element))
                .Trim()
                .Replace(" ", "");
        }

        return null;
    }

    public static string? ExtractDateOfIssue(MatchesResult matchesResult)
    {
        var dateOfIssueMatch = matchesResult.Matches?
            .FirstOrDefault(m => m.LabelGroupName == "DateOfIssue");

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