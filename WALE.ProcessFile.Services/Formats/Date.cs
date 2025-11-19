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
            || ContainsMonth(text)
            || DateTime.TryParse(text, out _);
    }

    private static bool ContainsMonth(string text)
    {
        var months = new List<string>
        {
            " Jan ",
            "January",
            " Feb ",
            "February",
            " Mar ",
            "March",
            " Apr ",
            "April",
            " May ",
            " Jun ",
            "June",
            " Jul ",
            "July",
            " Aug ",
            " August",
            " Sep ",
            " September",
            " Oct ",
            "October",
            " Nov ",
            "November",
            " Dec ",
            " December"
        };
        
        return months.Any(m => text.Contains(m, StringComparison.InvariantCultureIgnoreCase));
    }
    
    [GeneratedRegex(@"18\d\d|19\d\d|20\d\d")]
    private static partial Regex YearRegex();
}