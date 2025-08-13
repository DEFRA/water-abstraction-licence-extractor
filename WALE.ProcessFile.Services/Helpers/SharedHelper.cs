using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class SharedHelper
{
    public static string GetJson(MatchesResult matches)
    {
        DataHelper.NullOutSubLabels(matches.Matches!);
        return JsonSerializer.Serialize(matches, GetSerializer());
    }

    public static JsonSerializerOptions GetSerializer()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };
    }
}