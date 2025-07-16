using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class SharedHelper
{
    public static string GetJson(
        MatchesResult matches,
        string pdfFilePath)
    {
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
        
        DataHelpers.NullOutSubLabels(matches.Matches!);

        return JsonSerializer.Serialize(new ParseResult
        {
            Filename = pdfFilePath.Split('/').Last(),
            Matches = matches.Matches,
            NumberOfPages = matches.NumberOfPages,
        }, jsonOptions);
    }
}