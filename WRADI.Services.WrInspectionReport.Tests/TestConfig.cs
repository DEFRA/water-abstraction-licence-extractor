using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace WRADI.Services.WrInspectionReport.Tests;

public static class TestConfig
{
    [field: AllowNull, MaybeNull]
    private static IConfigurationRoot Config
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            field = new ConfigurationBuilder()
                .AddUserSecrets(typeof(TestConfig).Assembly)
                .Build();

            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string PdfFolder
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            field = Config["PdfFolder"]
                ?? throw new InvalidOperationException(
                    "PdfFolder user secret not set for WRADI.Services.WrInspectionReport.Tests. " +
                    "Run: dotnet user-secrets set PdfFolder \"<path>\" --project WRADI.Services.WrInspectionReport.Tests");

            return field;
        }
    }

    // Only needed for the opt-in table-based grid extraction golden-set run
    // (Wr51GroundTruthAccuracyTests) - null (not thrown) when unset, so every other test in
    // this project is unaffected by whether these are configured.
    [field: AllowNull, MaybeNull]
    public static string? AiServicesEndpoint => field ??= Config["AiServicesEndpoint"];

    [field: AllowNull, MaybeNull]
    public static string? AiServicesKey => field ??= Config["AiServicesKey"];
}
