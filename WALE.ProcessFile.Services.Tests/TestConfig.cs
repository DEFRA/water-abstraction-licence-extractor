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
}