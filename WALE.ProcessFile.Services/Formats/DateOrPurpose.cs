using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Formats;

public static partial class DateOrPurpose
{
    public static bool AnyIsDateOrPurpose(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        var returnValue = false;
        var outList = new List<DocumentLine>();
        
        foreach (var line in lines)
        {
            if (IsDateOrPurpose(line!.Text))
            {
                outList.Add(line);
                returnValue = true;
            }
        }

        matchedLines = outList;
        return returnValue;
    }
    
    public static bool IsDateOrPurpose(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Contains("aggregate"))
        {
            return true;
        }

        return YearRegex().IsMatch(text);
    }
    
    [GeneratedRegex(@"19\d\d|20\d\d")]
    private static partial Regex YearRegex();
}