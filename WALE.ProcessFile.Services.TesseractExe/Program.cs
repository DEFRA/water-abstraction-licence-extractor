using System.Globalization;
using Microsoft.Extensions.Configuration;
using Tesseract;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Services;
using TesseractOcrDataExtractorService = WALE.ProcessFile.Services.TesseractExe.TesseractOcrDataExtractorService;

var writeDebugLogs = true;

try
{
    var configuration = GetConfiguration();
    writeDebugLogs = configuration.GetValue<bool>("writeDebugLogs");
    
    var argsStringForLogging = string.Join(' ', args);
    if (args.Length >= 16)
    {
        var postgresPasswordTemp = args[15];
        argsStringForLogging = argsStringForLogging.Replace(postgresPasswordTemp, "*****");
    }
    
    await WriteLogFileIfDebugModeAsync(
        "Started.txt",
        $"{typeof(Program).Assembly.GetName().Name} started - " + argsStringForLogging,
        writeDebugLogs);

    if (args.Length < 16)
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
    var outputFolder = args[9];
    var tessDataPath = args[10];
    var postgresHost = args[11];
    var postgresPort = int.Parse(args[12]);
    var postgresDatabaseName = args[13];
    var postgresUsername = args[14];
    var postgresPassword = args[15];

    var isFileMode = bytesMode.Equals("file", StringComparison.InvariantCultureIgnoreCase);
    
    var (outputService, cacheService, tesseractService) = GetServices(
        isFileMode,
        pageSegMode,
        cacheFolder,
        outputFolder,
        tessDataPath,
        postgresHost,
        postgresPort,
        postgresDatabaseName,
        postgresUsername,
        postgresPassword);

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
        .AddEnvironmentVariables();

    return builder.Build();
}

static (IOutputService OutputService, ICacheService CacheService, TesseractOcrDataExtractorService TesseractService)
    GetServices(
        bool isFileMode,
        PageSegMode pageSegMode,
        string cacheFolder,
        string outputFolder,
        string tessDataPath,
        string postgresHost,
        int postgresPort,
        string postgresDatabaseName,
        string postgresUsername,
        string postgresPassword)
{
    if (string.IsNullOrEmpty(tessDataPath))
        throw new NullReferenceException(tessDataPath);
    
    var tesseractService = new TesseractOcrDataExtractorService(tessDataPath, pageSegMode);
    
    if (isFileMode)
    {
        var fileOutputService = new FileSystemOutputService(outputFolder);
        var fileCacheService = new FileSystemCacheService(cacheFolder);
        
        return (fileOutputService, fileCacheService, tesseractService);
    }
    
    if (string.IsNullOrEmpty(postgresHost))
        throw new NullReferenceException(nameof(postgresHost));
    
    if (string.IsNullOrEmpty(postgresDatabaseName))
        throw new NullReferenceException(nameof(postgresDatabaseName));
    
    if (string.IsNullOrEmpty(postgresUsername))
        throw new NullReferenceException(nameof(postgresUsername));
    
    if (string.IsNullOrEmpty(postgresPassword))
        throw new NullReferenceException(nameof(postgresPassword));
    
    var postgresDataSourceProvider = new NpgsqlDataSourceProvider(
        postgresHost,
        postgresPort,
        postgresDatabaseName,
        postgresUsername,
        postgresPassword);
    
    Dapper.DefaultTypeMap.MatchNamesWithUnderscores = true;

    var databaseReadService = new PostgresReadService(postgresDataSourceProvider);
    var databaseAddService = new PostgresWriteService(postgresDataSourceProvider);

    var dbOutputService = new DatabaseOutputService(databaseReadService, databaseAddService);
    var dbCacheService = new DatabaseCacheService(
        databaseReadService,
        databaseAddService,
        postgresHost,
        postgresPort,
        postgresDatabaseName,
        postgresUsername,
        postgresPassword);
    
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