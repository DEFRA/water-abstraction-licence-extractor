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

    public static Dictionary<string, object> MakeJsonElementDictionaryNative(
        Dictionary<string, object> inputDictionary)
    {
        var nativeDictionary = new Dictionary<string, object>();
            
        foreach (var kvp in inputDictionary)
        {
            object? value;
                
            if (kvp.Value is JsonElement jsonElement)
            {
                value = jsonElement.ValueKind switch
                {
                    JsonValueKind.Array => jsonElement.EnumerateArray().ToList(),
                    JsonValueKind.Number => jsonElement.GetInt32(), // NOTE - Used to be double
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Object => jsonElement.GetRawText(),
                    _ => throw new Exception($"Unexpected JSON value type {jsonElement.ValueKind}")
                };
            }
            else if (kvp.Value is int intValue)
            {
                value = intValue;
            }
            else if (kvp.Value is string strValue)
            {
                value = strValue;
            }
            else
            {
                throw new Exception($"Unknown type - {kvp.Value.GetType().Name}");
            }
                
            nativeDictionary.Add(kvp.Key, value!);
        }

        return nativeDictionary;
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