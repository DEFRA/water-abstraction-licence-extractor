using System.Text.RegularExpressions;
using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class Date
{
    public const string Constant = "Date";
    
    public static bool AnyIsDate(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        matchedLines = lines.Where(line => IsDate(line?.Text)).ToList()!;
        return matchedLines.Count > 0;
    }

    public static bool ContainsDate(string? text, out List<string> dates)
    {
        dates = [];
        
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }
        
        var words = text.Split(' ');

        foreach (var word in words)
        {
            if (DateTime.TryParse(word, out _))
            {
                dates.Add(word);
            }
        }

        return dates.Count > 0;
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