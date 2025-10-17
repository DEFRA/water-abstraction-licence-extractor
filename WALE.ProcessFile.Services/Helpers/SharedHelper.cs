using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class SharedHelper
{
    public static string GetJson(MatchesResult matches)
    {
        DataHelper.NullOutSubLabels(matches.Matches!);
        return JsonSerializer.Serialize(matches, JsonHelper.GetSerializerOptions());
    }
    
    public static string? DateFormatConsistent(string? input)
    {
        return input?.Replace(" ", string.Empty)
            .Replace("first", "1", StringComparison.InvariantCultureIgnoreCase)
            .Replace("second", "2", StringComparison.InvariantCultureIgnoreCase)
            .Replace("third", "3", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fourth", "4", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fifth", "5", StringComparison.InvariantCultureIgnoreCase)
            .Replace("sixth", "6", StringComparison.InvariantCultureIgnoreCase)
            .Replace("seventh", "7", StringComparison.InvariantCultureIgnoreCase)
            .Replace("eighth", "8", StringComparison.InvariantCultureIgnoreCase)
            .Replace("ninth", "9", StringComparison.InvariantCultureIgnoreCase)
            .Replace("tenth", "10", StringComparison.InvariantCultureIgnoreCase)
            .Replace("eleventh", "11", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twelfth", "12", StringComparison.InvariantCultureIgnoreCase)
            .Replace("thirteenth", "13", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fourteenth", "14", StringComparison.InvariantCultureIgnoreCase)
            .Replace("fifteenth", "15", StringComparison.InvariantCultureIgnoreCase)
            .Replace("sixteenth", "16", StringComparison.InvariantCultureIgnoreCase)
            .Replace("seventeenth", "17", StringComparison.InvariantCultureIgnoreCase)
            .Replace("eighteenth", "18", StringComparison.InvariantCultureIgnoreCase)
            .Replace("nineteenth", "19", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twentieth", "20", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-first", "21", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-second", "22", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-third", "23", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-fourth", "24", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-fifth", "25", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-sixth", "26", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-seventh", "27", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-eighth", "28", StringComparison.InvariantCultureIgnoreCase)
            .Replace("twenty-ninth", "29", StringComparison.InvariantCultureIgnoreCase)
            .Replace("thirtieth", "30", StringComparison.InvariantCultureIgnoreCase)
            .Replace("thirty-first", "31", StringComparison.InvariantCultureIgnoreCase)
            .Replace("August", "Aug", StringComparison.InvariantCultureIgnoreCase)
            .Replace("DAYOF", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("st", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("nd", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("rd", string.Empty, StringComparison.InvariantCultureIgnoreCase)
            .Replace("th", string.Empty, StringComparison.InvariantCultureIgnoreCase);
    }
}