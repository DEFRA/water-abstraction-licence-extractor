using Microsoft.Extensions.Configuration;

namespace WALE.ProcessFile.Services.Helpers;

public static class ConfigHelper
{
    public static string GetRequiredString(IConfiguration config, string key) =>
        config[key] ?? throw new NullReferenceException(key);

    public static int GetRequiredInt(IConfiguration config, string key) =>
        int.Parse(config[key] ?? throw new NullReferenceException(key));

    public static bool GetRequiredBool(IConfiguration config, string key) =>
        bool.Parse(config[key] ?? throw new NullReferenceException(key));

    public static bool? GetOptionalBool(IConfiguration config, string key)
    {
        var value = config[key];
        return string.IsNullOrWhiteSpace(value) ? null : bool.Parse(value);
    }

    public static int? GetOptionalInt(IConfiguration config, string key)
    {
        var value = config[key];
        return string.IsNullOrWhiteSpace(value) ? null : int.Parse(value);
    }
}