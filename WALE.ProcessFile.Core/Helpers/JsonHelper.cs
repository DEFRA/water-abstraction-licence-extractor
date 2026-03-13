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

    public static Dictionary<string, object?> MakeJsonElementDictionaryNative(
        Dictionary<string, object?> inputDictionary)
    {
        var nativeDictionary = new Dictionary<string, object?>();
            
        foreach (var kvp in inputDictionary)
        {
            var value = kvp.Value switch
            {
                int intValue => intValue,
                string strValue => strValue,
                null => null,
                JsonElement jsonElement => jsonElement.ValueKind switch
                {
                    JsonValueKind.Array => jsonElement.EnumerateArray().ToList(),
                    JsonValueKind.Number => GetSomeTypeOfNumber(jsonElement),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.String => jsonElement.GetString(),
                    JsonValueKind.Object => jsonElement.GetRawText(),
                    _ => throw new Exception($"Unexpected JSON value type {jsonElement.ValueKind}")
                },
                _ => throw new Exception($"Unknown type - {kvp.Value?.GetType().Name}")
            };

            nativeDictionary.Add(kvp.Key, value!);
        }

        return nativeDictionary;
    }

    private static object GetSomeTypeOfNumber(JsonElement jsonElement)
    {
        if (jsonElement.TryGetInt32(out var intValue))
        {
            return intValue;
        }
                            
        return jsonElement.GetDouble();
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