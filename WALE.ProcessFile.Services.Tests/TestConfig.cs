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
    
    private static string? _pdfFolder4;

    public static string PdfFolder4
    {
        get
        {
            if (_pdfFolder4 != null)
            {
                return _pdfFolder4;
            }
            
            _pdfFolder4 = Config["PdfFolder4"]!;
            return _pdfFolder4;
        }
    }
    
    private static string? _pdfFolder5;

    public static string PdfFolder5
    {
        get
        {
            if (_pdfFolder5 != null)
            {
                return _pdfFolder5;
            }
            
            _pdfFolder5 = Config["PdfFolder5"]!;
            return _pdfFolder5;
        }
    }
    
    private static string? _pdfFolderWr51;

    public static string PdfFolderWr51
    {
        get
        {
            if (_pdfFolderWr51 != null)
            {
                return _pdfFolderWr51;
            }
            
            _pdfFolderWr51 = Config["PdfFolderWr51"]!;
            return _pdfFolderWr51;
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
    
    private static string? _aiServicesEndpoint;

    public static string AiServicesEndpoint
    {
        get
        {
            if (_aiServicesEndpoint != null)
            {
                return _aiServicesEndpoint;
            }
            
            _aiServicesEndpoint = Config["AiServicesEndpoint"]!;
            return _aiServicesEndpoint;
        }
    }
    
    private static string? _aiServicesKey;

    public static string AiServicesKey
    {
        get
        {
            if (_aiServicesKey != null)
            {
                return _aiServicesKey;
            }
            
            _aiServicesKey = Config["AiServicesKey"]!;
            return _aiServicesKey;
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

    private static string? _postgresHost;
    public static string PostgresHost
    {
        get
        {
            if (_postgresHost != null)
            {
                return _postgresHost;
            }
            
            _postgresHost = Config["POSTGRESQL_HOST"]!;
            return _postgresHost;
        }
    }
    
    private static int? _postgresPort;
    public static int PostgresPort
    {
        get
        {
            if (_postgresPort != null)
            {
                return _postgresPort.Value;
            }
            
            _postgresPort = int.Parse(Config["POSTGRESQL_PORT"]!);
            return _postgresPort.Value;
        }
    }
    
    private static string? _postgresDbName;
    public static string PostgresDbName
    {
        get
        {
            if (_postgresDbName != null)
            {
                return _postgresDbName;
            }
            
            _postgresDbName = Config["POSTGRESQL_DBNAME"]!;
            return _postgresDbName;
        }
    }
    
    private static string? _postgresUsername;
    public static string PostgresUsername
    {
        get
        {
            if (_postgresUsername != null)
            {
                return _postgresUsername;
            }
            
            _postgresUsername = Config["POSTGRESQL_USERNAME"]!;
            return _postgresUsername;
        }
    }
    
    private static string? _postgresPassword;
    public static string PostgresPassword
    {
        get
        {
            if (_postgresPassword != null)
            {
                return _postgresPassword;
            }
            
            _postgresPassword = Config["POSTGRESQL_PASSWORD"]!;
            return _postgresPassword;
        }
    }
    
    private static string? _dotnetPath;

    public static string DotnetPath
    {
        get
        {
            if (_dotnetPath != null)
            {
                return _dotnetPath;
            }

            _dotnetPath = Config["DotnetPath"]!;
            return _dotnetPath;
        }
    }
    
    private static string? _tesseractExeName;

    public static string TesseractExeName
    {
        get
        {
            if (_tesseractExeName != null)
            {
                return _tesseractExeName;
            }

            _tesseractExeName = Config["TesseractExeName"]!;
            return _tesseractExeName;
        }
    }
    
    private static string? _tesseractExeDirectory;

    public static string TesseractExeDirectory
    {
        get
        {
            if (_tesseractExeDirectory != null)
            {
                return _tesseractExeDirectory;
            }

            _tesseractExeDirectory = Config["TesseractExeDirectory"]!;
            return _tesseractExeDirectory;
        }
    }
}