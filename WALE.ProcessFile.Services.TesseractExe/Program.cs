using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using NetMQ;
using NetMQ.Sockets;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Tesseract;

string? imageReference = null;
RequestSocket? client = null;

try
{
    var (portNumber, writeToConsole, writeToFile) = await SetupAsync(args);

    client = new RequestSocket($">tcp://localhost:{portNumber}");
    client.SendFrame(TesseractOcrDataExtractorService.ExeStartedOkay);
    
    var exeSharedLifecycleArgs = client.ReceiveFrameString();

    if (!exeSharedLifecycleArgs.StartsWith(TesseractOcrDataExtractorService.SharedArgsPrefix))
    {
        throw new Exception($"Tesseract server sent {exeSharedLifecycleArgs} when client expecting shared args");
    }

    var tesseractService = SplitExeSharedLifecycleArgs(
        exeSharedLifecycleArgs[TesseractOcrDataExtractorService.SharedArgsPrefix.Length..]);
    
    client.SendFrame(TesseractOcrDataExtractorService.TesseractSetupOkay);
    
    var perFileArgs = client.ReceiveFrameString();
    
    var (pageNumber,
        imageNumber,
        imageReferenceTemp,
        fileId,
        isPageScreenshot,
        processRunId) = SplitPerFileArgs(perFileArgs[TesseractOcrDataExtractorService.PerFileArgsPrefix.Length..]);
    
    imageReference = imageReferenceTemp;

    var textLines = await tesseractService.ProcessAsync(
        "PdfPig",
        pageNumber,
        imageNumber,
        isPageScreenshot,
        imageReference,
        fileId,
        processRunId);
    
    await WriteResponseAsync(
        "Finished.txt",
        $"{typeof(Program).Assembly.GetName().Name} finished with {textLines.Count} rows - {exeSharedLifecycleArgs}",
        writeToConsole,
        writeToFile);

    var dataStr = JsonSerializer.Serialize(textLines);
    client.SendFrame($"{TesseractOcrDataExtractorService.FinishedPrefix}{dataStr}");
}
catch (Exception ex)
{
    await WriteResponseAsync(
        "Error.txt",
         $"{ex} - {imageReference}",
        true,
        true,
        true);
    
    client?.SendFrame($"{TesseractOcrDataExtractorService.ErroredPrefix}: {ex}");
    Environment.ExitCode = 1;
}

return;

static InternalTesseractOcrDataExtractorService SplitExeSharedLifecycleArgs(string argsStr)
{
    var args = argsStr.Split(' ');
    
    var pageSegMode = Enum.Parse<PageSegMode>(args[0]);
    var bytesMode = args[1];
    var cacheFolderOrApiUrl = args[2].Replace("\"", string.Empty);
    var outputFolder = args[3].Replace("\"", string.Empty);
    var tessDataPath = args[4].Replace("\"", string.Empty);

    var isFileMode = bytesMode.Equals("file", StringComparison.OrdinalIgnoreCase);
    
    return GetTesseractService(
        isFileMode,
        pageSegMode,
        cacheFolderOrApiUrl,
        outputFolder,
        tessDataPath);
}

static (int pageNumber,
    int imageNumber,
    string imageReference,
    Guid fileId,
    bool isPageScreenshot,
    int processRunId) SplitPerFileArgs(string argsStr)
{
    var args = argsStr.Split(' ');
    
    var pageNumber = int.Parse(args[0]);
    var imageNumber = int.Parse(args[1]);
    var imageReference = args[2];
    var fileId = Guid.Parse(args[3]);
    var isPageScreenshot = bool.Parse(args[4]);
    var processRunId = int.Parse(args[5]);

    return (
        pageNumber,
        imageNumber,
        imageReference,
        fileId,
        isPageScreenshot,
        processRunId);
}

static async Task<(int PortNumber, bool WriteToConsole, bool WriteToFile)> SetupAsync(string[] args)
{
    var configuration = GetConfiguration();

    var writeToConsole = configuration.GetValue<bool?>("writeToConsole") ?? true;
    var writeToFile = configuration.GetValue<bool?>("writeDebugLogs") ?? false;
    
    var argsStringForLogging = string.Join(' ', args);

    await WriteResponseAsync(
        "Started.txt",
        $"{typeof(Program).Assembly.GetName().Name} started - " + argsStringForLogging,
        writeToConsole,
        writeToFile);

    if (args.Length != 1)
    {
        await WriteResponseAsync(
            "Error.txt",
            "Not enough / too many arguments provided",
            true,
            true,
            true);

        Environment.ExitCode = 1;
        throw new Exception("Not enough / too many arguments provided");
    }
    
    return (int.Parse(args[0]), writeToConsole, writeToFile);
}

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
    PageSegMode pageSegMode,
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

static async Task WriteResponseAsync(
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