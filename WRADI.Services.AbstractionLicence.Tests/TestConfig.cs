using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.Configuration;
using WRADI.Services.AbstractionLicence.Tests.IntegrationTests;

namespace WRADI.Services.AbstractionLicence.Tests;

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
                .AddUserSecrets<TesseractAndAzureAiVisionOcrPdfTests>()
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
            
            field = Config["PdfFolder"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string PdfFolder2
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            
            field = Config["PdfFolder2"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string PdfFolder3
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            
            field = Config["PdfFolder3"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string PdfFolder4
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            
            field = Config["PdfFolder4"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string PdfFolder5
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            
            field = Config["PdfFolder5"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string AiVisionEndpoint
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            
            field = Config["AiVisionEndpoint"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string AiVisionKey
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            
            field = Config["AiVisionKey"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string TesseractPath
    {
        get
        {
            if (field != null)
            {
                return field;
            }
            
            field = Config["TesseractPath"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string DotnetPath
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            field = Config["DotnetPath"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string TesseractExeName
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            field = Config["TesseractExeName"]!;
            return field;
        }
    }

    [field: AllowNull, MaybeNull]
    public static string TesseractExeDirectory
    {
        get
        {
            if (field != null)
            {
                return field;
            }

            field = Config["TesseractExeDirectory"]!;
            return field;
        }
    }
}