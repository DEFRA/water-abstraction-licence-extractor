using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class DateOrPurpose
{
    public static bool AnyIsDateOrPurpose(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = lines.Where(line => IsDateOrPurpose(line?.Text)).ToList()!;
        return matchedLines.Count > 0;
    }
    
    public static bool IsDateOrPurpose(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        const string purposeWord = "aggregate";
        return text.Contains(purposeWord) || YearRegex().IsMatch(text);
    }
    
    [GeneratedRegex(@"19\d\d|20\d\d")]
    private static partial Regex YearRegex();
}