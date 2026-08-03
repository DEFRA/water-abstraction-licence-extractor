using System.Text.Json;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static class JsonHelper
{
    public static string GetAsString(MatchesResult matches)
    {
        FormattingHelper.NullOutSubLabels(matches.Matches!);
        return JsonSerializer.Serialize(matches, GetSerializerOptions());
    }
    
    public static Dictionary<string, object?> MakeJsonElementDictionaryNative(
        Dictionary<string, object?> inputDictionary)
    {
        var nativeDictionary = new Dictionary<string, object?>();
            
        foreach (var kvp in inputDictionary)
        {
            var value = CastFromJsonTypeToNative(kvp.Value);
            nativeDictionary.Add(kvp.Key, value!);
        }

        return nativeDictionary;
    }

    public static object? CastFromJsonTypeToNative(object? input)
    {
        return input switch
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
                JsonValueKind.Undefined => null,
                JsonValueKind.Null => null,
                _ => throw new Exception($"Unexpected JSON value type {jsonElement.ValueKind}")
            },
            _ => throw new Exception($"Unknown type - {input?.GetType().Name}")
        };
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