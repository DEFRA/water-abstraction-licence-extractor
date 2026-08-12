namespace WRADI.ProcessFile.Cmd.AbstractionLicence;

using Microsoft.Extensions.Configuration;

public static class ConfigurationExtensions
{
    public static T GetRequiredValue<T>(
        this IConfiguration configuration,
        string key)
    {
        var rawValue = configuration[key];

        if (string.IsNullOrWhiteSpace(rawValue))
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' is missing.");
        }

        try
        {
            var value = configuration.GetValue<T>(key);

            if (value is null)
            {
                throw new InvalidOperationException(
                    $"Configuration value '{key}' is invalid.");
            }

            return value;
        }
        catch (Exception exception)
            when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"Configuration value '{key}' could not be converted to " +
                $"'{typeof(T).Name}'.",
                exception);
        }
    }
}