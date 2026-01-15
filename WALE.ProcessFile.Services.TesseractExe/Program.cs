using System.Globalization;
using Microsoft.Extensions.Configuration;
using Tesseract;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Services;
using TesseractOcrDataExtractorService = WALE.ProcessFile.Services.TesseractExe.TesseractOcrDataExtractorService;

// NOTE - Following lines are just for debugging
/*args = [
    "SparseTextOsd", // Tesseract mode
    "Database", // DB or file
    //"2", // Page number
    "1", // Page number
    "1", // Image number
    //"ImageReference-83743S0057__8-37-43-S-0057Plans-png-2-1", // Image reference (for extension)
    "ImageReference-83743S0057__8-37-43-S-0057Plans-png-1-1", // Image reference (for extension)
    "83743S0057__8-37-43-S-0057Plans", // PDF filename (without .pdf)
    "false", // Is page screenshot
    "500", // processRunId
    "Cache/"
];*/

const bool writeDebugLogs = true;

try
{
    await WriteLogFileIfDebugModeAsync(
        "Started.txt",
        $"{typeof(Program).Assembly.GetName().Name} started - " + string.Join(' ', args),
        writeDebugLogs);

    if (args.Length < 9)
    {
        throw new Exception("Not enough arguments provided");
    }

    var pageSegMode = Enum.Parse<PageSegMode>(args[0]);
    var bytesMode = args[1];
    var pageNumber = int.Parse(args[2]);
    var imageNumber = int.Parse(args[3]);
    var imageReference = args[4];
    var pdfFilepath = args[5];
    var isPageScreenshot = bool.Parse(args[6]);
    var processRunId = int.Parse(args[7]);
    var cacheFolder = args[8];

    var isFileMode = bytesMode.Equals("file", StringComparison.InvariantCultureIgnoreCase);
    
    var (outputService, cacheService, tesseractService) = GetServices(
        isFileMode,
        pageSegMode,
        cacheFolder);

    byte[]? imageBytes;

    if (isPageScreenshot)
    {
        imageBytes = await outputService.GetPageScreenshotDataAsync(
            pageNumber,
            PdfDataExtractorService.Name,
            pdfFilepath);
    }
    else
    {
        imageBytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilepath,
            NoOcrServiceName = PdfDataExtractorService.Name,
            Extension = FileHelper.GetImageExtension(imageReference)
        });
    }

    if (imageBytes == null)
    {
        throw new Exception("Image was not found");
    }
    
    var textLines = tesseractService.GetDataFromTesseract(imageBytes);
    
    var request = new OcrServiceImageTextCacheRequest
    {
        PageNumber = pageNumber,
        ImageNumber = imageNumber,
        Filepath = pdfFilepath,
        OcrServiceName = $"TesseractOcr-{pageSegMode}",
        ProcessRunId = processRunId
    };

    if (isPageScreenshot)
    {
        await cacheService.SaveTemporaryOcrScreenshotTextAsync(request, textLines);        
    }
    else
    {
        await cacheService.SaveTemporaryOcrImageTextAsync(request, textLines);        
    }
    
    await WriteLogFileIfDebugModeAsync(
        "Finished.txt",
        $"{typeof(Program).Assembly.GetName().Name} finished with {textLines.Count} rows",
        writeDebugLogs);
}
catch (Exception ex)
{
    await WriteLogFileIfDebugModeAsync(
        "Error.txt",
         "ERROR - " + ex,
        writeDebugLogs);

    throw;
}

return;

static IConfiguration GetConfiguration()
{
    var builder = new ConfigurationBuilder();
    builder.SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: true)        
        .AddEnvironmentVariables();

    return builder.Build();
}

static (IOutputService OutputService, ICacheService CacheService, TesseractOcrDataExtractorService TesseractService)
    GetServices(bool isFileMode, PageSegMode pageSegMode, string cacheFolder)
{
    var configuration = GetConfiguration();
    var tessDataPath = configuration.GetValue<string>("TESSDATA_PREFIX");
    if (string.IsNullOrEmpty(tessDataPath))
        throw new NullReferenceException("TESSDATA_PREFIX");
    
    var tesseractService = new TesseractOcrDataExtractorService(tessDataPath, pageSegMode);
    
    if (isFileMode)
    {
        var outputFolder = configuration.GetValue<string>("OutputFolder")
            ?? throw new NullReferenceException("OutputFolder");
        
        var fileOutputService = new FileSystemOutputService(outputFolder);
        var fileCacheService = new FileSystemCacheService(cacheFolder);
        
        return (fileOutputService, fileCacheService, tesseractService);
    }
    
    var postgresConnectionString = configuration.GetValue<string>("PostgresConnectionString");
    if (string.IsNullOrEmpty(postgresConnectionString))
        throw new NullReferenceException("PostgresConnectionString");
    
    var postgresDataSourceProvider = new NpgsqlDataSourceProvider(postgresConnectionString);
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

    var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
    var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);

    var dbOutputService = new DatabaseOutputService(databaseReadService, databaseAddService);
    var dbCacheService = new DatabaseCacheService(databaseReadService, databaseAddService);
    
    return (dbOutputService, dbCacheService, tesseractService);
}

static async Task WriteLogFileIfDebugModeAsync(string filename, string content, bool isDebug)
{
    Console.WriteLine(content);
    
    if (!isDebug)
    {
        return;
    }
    
    await File.WriteAllTextAsync(
        filename,
        content + "\n" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
}