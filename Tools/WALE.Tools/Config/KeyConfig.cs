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
}