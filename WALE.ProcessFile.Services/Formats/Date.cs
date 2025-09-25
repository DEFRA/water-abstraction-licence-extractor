using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class Date
{
    public const string Constant = "Date";
    
    public static bool AnyIsDate(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = lines.Where(line => IsDate(line?.Text)).ToList()!;

        if (matchedLines.Count > 0)
        {
            
        }
        
        return matchedLines.Count > 0;
    }
    
    public static bool IsDate(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        
        return YearRegex().IsMatch(text)
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