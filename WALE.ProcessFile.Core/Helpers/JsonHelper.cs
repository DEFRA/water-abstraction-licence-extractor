using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Helpers;

public static class JsonHelper
{
    public static string GetAsString(MatchesResult matches)
    {
        FormattingHelper.NullOutSubLabels(matches.Matches!);
        return JsonSerializer.Serialize(matches, GetSerializerOptions());
    }
    
    public static string GetAsString(Licence licence)
    {
        return JsonSerializer.Serialize(licence, GetSerializerOptions());
    }
    
    public static string GetAsString(Dictionary<string, LicenceSet> licenceSets)
    {
        return JsonSerializer.Serialize(licenceSets.Values, GetSerializerOptions());
    }
    
    public static JsonSerializerOptions GetSerializerOptions()
    {
        _options ??= new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        return _options;
    }
    
    private static JsonSerializerOptions? _options;
}