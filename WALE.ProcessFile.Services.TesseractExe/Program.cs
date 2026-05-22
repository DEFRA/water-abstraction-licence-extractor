using System.Globalization;
using Microsoft.Extensions.Configuration;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Tesseract;

string? imageReference = null;

try
{
    var configuration = GetConfiguration();

    var writeToConsole = configuration.GetValue<bool?>("writeToConsole") ?? true;
    var writeToFile = configuration.GetValue<bool>("writeDebugLogs");
    
    var argsStringForLogging = string.Join(' ', args);

    await WriteLogFileIfDebugModeAsync(
        "Started.txt",
        $"{typeof(Program).Assembly.GetName().Name} started - " + argsStringForLogging,
        writeToConsole,
        writeToFile);

    if (args.Length < 11)
    {
        await WriteLogFileIfDebugModeAsync(
            "Error.txt",
            "Not enough arguments provided",
            true,
            true,
            true);

        return;
    }

    var pageSegMode = Enum.Parse<WALE.ProcessFile.Core.Enums.PageSegMode>(args[0]);
    var bytesMode = args[1];
    var pageNumber = int.Parse(args[2]);
    var imageNumber = int.Parse(args[3]);
    imageReference = args[4];
    var fileId = Guid.Parse(args[5]);
    var isPageScreenshot = bool.Parse(args[6]);
    var processRunId = int.Parse(args[7]);
    var cacheFolderOrApiUrl = args[8];
    var outputFolder = args[9];
    var tessDataPath = args[10];

    var isFileMode = bytesMode.Equals("file", StringComparison.InvariantCultureIgnoreCase);
    
    var tesseractService = GetTesseractService(
        isFileMode,
        pageSegMode,
        cacheFolderOrApiUrl,
        outputFolder,
        tessDataPath);

    var textLines = await tesseractService.ProcessAsync(
        "PdfPig",
        pageNumber,
        imageNumber,
        isPageScreenshot,
        imageReference,
        fileId,
        processRunId);
    
    await WriteLogFileIfDebugModeAsync(
        "Finished.txt",
        $"{typeof(Program).Assembly.GetName().Name} finished with {textLines.Count} rows",
        writeToConsole,
        writeToFile);
}
catch (Exception ex)
{
    await WriteLogFileIfDebugModeAsync(
        "Error.txt",
         $"{ex} - {imageReference}",
        true,
        true,
        true);
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

static InternalTesseractOcrDataExtractorService GetTesseractService(
    bool isFileMode,
    WALE.ProcessFile.Core.Enums.PageSegMode pageSegMode,
    string cacheFolderOrApiUrl,
    string outputFolder,
    string tessDataPath)
{
    if (string.IsNullOrEmpty(tessDataPath))
    {
        throw new NullReferenceException(tessDataPath);
    }

    ICacheService cacheService;
    IOutputService outputService;
    
    if (isFileMode)
    {
        outputService = new FileSystemOutputService(outputFolder);
        cacheService = new FileSystemCacheService(cacheFolderOrApiUrl);
    }
    else
    {
        if (string.IsNullOrEmpty(cacheFolderOrApiUrl))
        {
            throw new NullReferenceException(nameof(cacheFolderOrApiUrl));
        }

        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(cacheFolderOrApiUrl);
        
        outputService = new ApiOutputService(httpClient);
        cacheService = new ApiCacheService(httpClient);
    }
    
    return new InternalTesseractOcrDataExtractorService(
        outputService,
        cacheService,
        tessDataPath,
        pageSegMode);
}

static async Task WriteLogFileIfDebugModeAsync(
    string filename,
    string content,
    bool shouldConsoleWrite,
    bool shouldWriteFile,
    bool isError = false)
{
    if (shouldConsoleWrite)
    {
        var type = isError ? "ERROR" : "INFO";
        ConsoleHelper.WriteLine($"{type} - TesseractExe - {content}");
    }

    if (!shouldWriteFile)
    {
        return;
    }
    
    await File.WriteAllTextAsync(
        filename,
        content + "\n" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
}