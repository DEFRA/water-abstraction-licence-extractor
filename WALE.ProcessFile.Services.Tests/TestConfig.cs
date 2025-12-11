using Microsoft.Extensions.Configuration;
using WALE.ProcessFile.Services.Tests.IntegrationTests;

namespace WALE.ProcessFile.Services.Tests;

public static class TestConfig
{
    private static IConfigurationRoot? _config;

    private static IConfigurationRoot Config
    {
        get
        {
            if (_config != null)
            {
                return _config;
            }
            
            _config = new ConfigurationBuilder()
                .AddUserSecrets<PdfPigNoOcrPdfTests>()
                .Build();
            
            return _config;
        }
    }
    
    private static string? _pdfFolder;

    public static string PdfFolder
    {
        get
        {
            if (_pdfFolder != null)
            {
                return _pdfFolder;
            }
            
            _pdfFolder = Config["PdfFolder"]!;
            return _pdfFolder;
        }
    }
    
    private static string? _pdfFolder2;

    public static string PdfFolder2
    {
        get
        {
            if (_pdfFolder2 != null)
            {
                return _pdfFolder2;
            }
            
            _pdfFolder2 = Config["PdfFolder2"]!;
            return _pdfFolder2;
        }
    }
    
    private static string? _pdfFolder3;

    public static string PdfFolder3
    {
        get
        {
            if (_pdfFolder3 != null)
            {
                return _pdfFolder3;
            }
            
            _pdfFolder3 = Config["PdfFolder3"]!;
            return _pdfFolder3;
        }
    }
    
    private static string? _aiVisionEndpoint;

    public static string AiVisionEndpoint
    {
        get
        {
            if (_aiVisionEndpoint != null)
            {
                return _aiVisionEndpoint;
            }
            
            _aiVisionEndpoint = Config["AiVisionEndpoint"]!;
            return _aiVisionEndpoint;
        }
    }
    
    private static string? _aiVisionKey;

    public static string AiVisionKey
    {
        get
        {
            if (_aiVisionKey != null)
            {
                return _aiVisionKey;
            }
            
            _aiVisionKey = Config["AiVisionKey"]!;
            return _aiVisionKey;
        }
    }
    
    private static string? _awsAccessKey;

    public static string AwsAccessKey
    {
        get
        {
            if (_awsAccessKey != null)
            {
                return _awsAccessKey;
            }
            
            _awsAccessKey = Config["AwsAccessKey"]!;
            return _awsAccessKey;
        }
    }
    
    private static string? _awsSecretKey;

    public static string AwsSecretKey
    {
        get
        {
            if (_awsSecretKey != null)
            {
                return _awsSecretKey;
            }
            
            _awsSecretKey = Config["AwsSecretKey"]!;
            return _awsSecretKey;
        }
    }
    
    private static string? _openAiEndpoint;

    public static string OpenAiEndpoint
    {
        get
        {
            if (_openAiEndpoint != null)
            {
                return _openAiEndpoint;
            }
            
            _openAiEndpoint = Config["OpenAiEndpoint"]!;
            return _openAiEndpoint;
        }
    }
    
    private static string? _openAiKey;

    public static string OpenAiKey
    {
        get
        {
            if (_openAiKey != null)
            {
                return _openAiKey;
            }
            
            _openAiKey = Config["OpenAiKey"]!;
            return _openAiKey;
        }
    }

    private static string? _openAiModelName;

    public static string OpenAiModelName
    {
        get
        {
            if (_openAiModelName != null)
            {
                return _openAiModelName;
            }
            
            _openAiModelName = Config["OpenAiModelName"]!;
            return _openAiModelName;
        }
    }

    private static string? _openAiDeploymentName;

    public static string OpenAiDeploymentName
    {
        get
        {
            if (_openAiDeploymentName != null)
            {
                return _openAiDeploymentName;
            }
            
            _openAiDeploymentName = Config["OpenAiDeploymentName"]!;
            return _openAiDeploymentName;
        }
    }
    
    private static string? _tesseractPath;

    public static string TesseractPath
    {
        get
        {
            if (_tesseractPath != null)
            {
                return _tesseractPath;
            }
            
            _tesseractPath = Config["TesseractPath"]!;
            return _tesseractPath;
        }
    }

    private static string? _sqlConnectionString;
    public static string SqlConnectionString
    {
        get
        {
            if (_sqlConnectionString != null)
            {
                return _sqlConnectionString;
            }
            
            _sqlConnectionString = Config["SqlConnectionString"]!;
            return _sqlConnectionString;
        }
    }

    private static string? _postgresConnectionString;
    public static string PostgresConnectionString
    {
        get
        {
            if (_postgresConnectionString != null)
            {
                return _postgresConnectionString;
            }
            
            _postgresConnectionString = Config["PostgresConnectionString"]!;
            return _postgresConnectionString;
        }
    }
}