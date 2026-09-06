using System.Text.RegularExpressions;
using WALE.Tools._2ndHalf.Models;

namespace WALE.Tools._2ndHalf;

public static class PurposeMapperLlm
{
    private const string SpaceString = " ";
    
    public static void MapPurposes(string naldPurposesPath, string documentPurposesPath)
    {
        var naldPurposes = GetNaldPurposes(naldPurposesPath);
        var documentPurposes = GetDocumentPurposes(documentPurposesPath);

        var resultDict = new Dictionary<string, PurposeMatch[]>();

        foreach (var documentPurpose in documentPurposes)
        {
            var normalisedDocumentPurpose = NormalizeText(documentPurpose);

            var matches = naldPurposes
                .Where(naldPurpose =>
                    // One contains the other
                    naldPurpose.NormalisedDescription.Contains(normalisedDocumentPurpose)
                    || normalisedDocumentPurpose.Contains(naldPurpose.NormalisedDescription)
                    // At least one token overlaps
                    || TokenOverlapCount(naldPurpose.NormalisedDescription, normalisedDocumentPurpose) >= 1
                    || naldPurpose.NormalisedDescription.StartsWith(normalisedDocumentPurpose)
                    || naldPurpose.NormalisedDescription.EndsWith(normalisedDocumentPurpose)
                    || normalisedDocumentPurpose.StartsWith(naldPurpose.NormalisedDescription)
                    || normalisedDocumentPurpose.EndsWith(naldPurpose.NormalisedDescription)
                )
                .Select(c => new PurposeMatch { Name = c.Description, Code = c.Code })
                .ToArray();

            resultDict[documentPurpose] = matches;
        }

        // TODO save
        Console.WriteLine($"{resultDict.Count} purposes");
    }

    private static string[] GetDocumentPurposes(string documentPurposesPath)
    {
        if (!File.Exists(documentPurposesPath))
        {
            throw new FileNotFoundException("Document purposes txt file not found", documentPurposesPath);
        }
        
        return File.ReadAllLines(documentPurposesPath)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
    
    private static List<(
        string Code,
        string Description,
        string NormalisedDescription)> GetNaldPurposes(string naldPurposesPath)
    {
        if (!File.Exists(naldPurposesPath))
        {
            throw new FileNotFoundException("Nald purposes csv file not found", naldPurposesPath);
        }
        
        var naldPurposesLines = File.ReadAllLines(naldPurposesPath)
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // If first line looks like a header (contains "uniquecode"), skip it
        if (naldPurposesLines.Count > 0
            && naldPurposesLines[0].Contains("uniquecode", StringComparison.InvariantCultureIgnoreCase))
        {
            naldPurposesLines = naldPurposesLines.Skip(1).ToList();
        }

        var naldPurposes = new List<(
            string Code,
            string Description,
            string NormalisedDescription)>();

        foreach (var line in naldPurposesLines)
        {
            var parts = SplitCsvLine(line);

            if (parts.Length < 2)
            {
                continue;
            }

            var code = parts[0].Trim();
            var desc = parts[1].Trim();
            var normalisedDescription = NormalizeText(desc);

            naldPurposes.Add((
                Code: code,
                Description: desc,
                NormalisedDescription: normalisedDescription));
        }
        
        return naldPurposes;
    }

    private static string NormalizeText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }
        
        text = text.ToLowerInvariant();

        // Replace punctuation with spaces
        text = Regex.Replace(text, @"[^\w\s]", " ");

        // Collapse multiple spaces
        text = Regex.Replace(text, @"\s+", " ").Trim();

        // Remove common stopwords that often vary between lists
        var stopwords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "and","&","the","of","for","to","a","an","in","on","with","other","use","uses","purpose","purposes",
            "subsequent","subsequently","including","including","general","maintain","maintaining","throughflow",
            "through","flow","supply","supply","water","makeup","make-up","top-up","topup","topping","up","storage",
            "direct","storage","domestic","commercial","industrial","public","private","other","than","(other","other)"
        };

        var tokens = text.Split(' ')
            .Where(t => !string.IsNullOrWhiteSpace(t) && !stopwords.Contains(t))
            .ToArray();

        return string.Join(" ", tokens);
    }

    // Count overlapping tokens between two normalized strings
    private static int TokenOverlapCount(string a, string b)
    {
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
        {
            return 0;
        }

        var tokensA = a.Split(SpaceString)
            .Where(t => t.Length > 0).
            Distinct()
            .ToArray();
        
        var tokensB = b.Split(SpaceString)
            .Where(t => t.Length > 0)
            .Distinct()
            .ToArray();
        
        return tokensA.Intersect(tokensB).Count();
    }

    // Simple CSV splitter that handles quoted fields
    private static string[] SplitCsvLine(string line)
    {
        // Split on commas not inside quotes
        var pattern = ",(?=(?:[^\"]*\"[^\"]*\")*[^\"]*$)";

        var parts = Regex.Split(line, pattern)
            .Select(p => p.Trim().Trim('"'))
            .ToArray();

        return parts;
    }
}