using Microsoft.Extensions.Configuration;

namespace WALE.Tools.Config;

public static class KeyConfig
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
                .AddUserSecrets<Program>()
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
    
    private static string? _tesseractPrefix;

    public static string TesseractPrefix
    {
        get
        {
            if (_tesseractPrefix != null)
            {
                return _tesseractPrefix;
            }
            
            _tesseractPrefix = Config["TESSDATA_PREFIX"]!;
            return _tesseractPrefix;
        }
    }
    
    private static string? _outputFolder;

    public static string OutputFolder
    {
        get
        {
            if (_outputFolder != null)
            {
                return _outputFolder;
            }
            
            _outputFolder = Config["OutputFolder"]!;
            return _outputFolder;
        }
    }
    
    private static string? _cacheFolder;

    public static string CacheFolder
    {
        get
        {
            if (_cacheFolder != null)
            {
                return _cacheFolder;
            }
            
            _cacheFolder = Config["CacheFolder"]!;
            return _cacheFolder;
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

    private static string? _pdfFolderForDuplicates;

    public static string PdfFolderForDuplicates
    {
        get
        {
            if (_pdfFolderForDuplicates != null)
            {
                return _pdfFolderForDuplicates;
            }
            
            _pdfFolderForDuplicates = Config["PdfFolderForDuplicates"]!;
            return _pdfFolderForDuplicates;
        }
    }
    
    private static string? _pdfFolderForOverrides;

    public static string PdfFolderForOverrides
    {
        get
        {
            if (_pdfFolderForOverrides != null)
            {
                return _pdfFolderForOverrides;
            }
            
            _pdfFolderForOverrides = Config["PdfFolderForOverrides"]!;
            return _pdfFolderForOverrides;
        }
    }

    private static string? _naldDataDumpFolder;

    public static string NaldDataDumpFolder
    {
        get
        {
            if (_naldDataDumpFolder != null)
            {
                return _naldDataDumpFolder;
            }

            _naldDataDumpFolder = Config["NaldDataDumpFolder"]!;
            return _naldDataDumpFolder;
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

    private static string? _apiBaseUrl;
    
    public static string ApiBaseUrl
    {
        get
        {
            if (_apiBaseUrl != null)
            {
                return _apiBaseUrl;
            }

            _apiBaseUrl = Config["ApiBaseUrl"]!;
            return _apiBaseUrl;
        }
    }

    private static string? _awsAccessKey;
    
    public static string AwsS3AccessKey
    {
        get
        {
            if (_awsAccessKey != null)
            {
                return _awsAccessKey;
            }

            _awsAccessKey = Config["AwsS3AccessKey"]!;
            return _awsAccessKey;
        }
    }
    
    private static string? _awsSecretKey;
    
    public static string AwsS3SecretKey
    {
        get
        {
            if (_awsSecretKey != null)
            {
                return _awsSecretKey;
            }

            _awsSecretKey = Config["AwsS3SecretKey"]!;
            return _awsSecretKey;
        }
    }
    
    private static string? _awsS3BucketName;
    
    public static string AwsS3BucketName
    {
        get
        {
            if (_awsS3BucketName != null)
            {
                return _awsS3BucketName;
            }

            _awsS3BucketName = Config["AwsS3BucketName"]!;
            return _awsS3BucketName;
        }
    }
    
    private static string? _awsS3ZipPassword;
    
    public static string AwsS3ZipPassword
    {
        get
        {
            if (_awsS3ZipPassword != null)
            {
                return _awsS3ZipPassword;
            }

            _awsS3ZipPassword = Config["AwsS3ZipPassword"]!;
            return _awsS3ZipPassword;
        }
    }
}