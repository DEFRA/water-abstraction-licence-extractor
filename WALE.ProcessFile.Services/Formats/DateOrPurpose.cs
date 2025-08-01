using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class DateOrPurpose
{
    public const string Constant = "DateOrPurpose";
    
    public static bool AnyIsDateOrPurpose(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = lines.Where(line => IsDateOrPurpose(line?.Text)).ToList()!;
        return matchedLines.Count > 0;
    }
    
    private static bool IsDateOrPurpose(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        const string purposeWord = "aggregate";
        
        return text.Contains(purposeWord)
               || YearRegex().IsMatch(text)
               || ContainsMonth(text);
    }

    private static bool ContainsMonth(string text)
    {
        var months = new List<string>
        {
            "January",
            "February",
            "March",
            "April",
            "May",
            "June",
            "July",
            "August",
            "September",
            "October",
            "November",
            "December"
        };
        
        return months.Any(text.Contains);
    }
    
    [GeneratedRegex(@"19\d\d|20\d\d")]
    private static partial Regex YearRegex();
}