using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static class DateOrPurpose
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
        return text.Contains(purposeWord) || Date.IsDate(text);
    }
}