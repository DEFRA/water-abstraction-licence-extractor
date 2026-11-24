using System.Globalization;
using Scalar.AspNetCore;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Tesseract;
using WALE.ProcessFile.Services.TesseractExe;
using WALE.ProcessFile.Services.TesseractExe.Models;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var app = builder.Build();

//if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseHttpsRedirection();
var (portNumber, writeToConsole, writeToFile) = await SetupAsync(args);
var tesseractSetupLock = new Lock();

app.MapPost("/setup", (SetupTesseractRequest request) =>
    {
        if (TesseractService.Instance != null)
        {
            return Results.Ok();
        }
        
        lock (tesseractSetupLock)
        {
            TesseractService.Instance ??= 
                GetTesseractService(
                    request.BytesMode?.Equals("file", StringComparison.OrdinalIgnoreCase) == true,
                    request.PageSegMode,
                    request.CacheFolderOrApiUrl!,
                    request.OutputFolder!,
                    request.TessDataPath!);
        }

        return Results.Ok();
    })
.WithName("SetupTesseractAsync");

app.MapPost("/scan", async (ScanFileRequest request) =>
    {
        var textLines = await TesseractService.Instance!.ProcessAsync(
            GeneralConstants.PdfPigDataExtractorServiceName,
            request.PageNumber,
            request.ImageNumber,
            request.IsPageScreenshot,
            request.ImageReference!,
            request.FileId,
            request.ProcessRunId);
        
        await LogAsync(
            $"Finished with {textLines.Count} rows - {request.FileId} P{request.PageNumber}",
            writeToConsole,
            writeToFile,
            "Finished.txt");
        
        return Results.Ok(textLines);
    })
    .WithName("ScanFileAsync");

app.Run($"http://localhost:{portNumber}");

return;

static InternalTesseractOcrDataExtractorService GetTesseractService(
    bool isFileMode,
    PageSegMode pageSegMode,
    string cacheFolderOrApiUrl,
    string outputFolder,
    string tessDataPath)
{
    if (string.IsNullOrEmpty(tessDataPath))
    {
        throw new Exception($"{nameof(tessDataPath)} is null or empty");
    }
    
    if (string.IsNullOrEmpty(cacheFolderOrApiUrl))
    {
        throw new Exception($"{nameof(cacheFolderOrApiUrl)} is null or empty");
    }

    ICacheService cacheService;
    IOutputService outputService;
    
    if (isFileMode)
    {
        if (string.IsNullOrEmpty(outputFolder))
        {
            throw new Exception($"{nameof(outputFolder)} is null or empty");
        }
        
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

static async Task<(int PortNumber, bool WriteToConsole, bool WriteToFile)> SetupAsync(string[] args)
{
    var configuration = GetConfiguration();

    var writeToConsole = configuration.GetValue<bool?>("writeToConsole") ?? true;
    var writeToFile = configuration.GetValue<bool?>("writeDebugLogs") ?? false;
    
    var argsStringForLogging = string.Join(' ', args);

    if (args.Length != 1)
    {
        // Next line for debugging
        return (1234, writeToConsole, writeToFile);
        
        await LogAsync(
            "Not enough / too many arguments provided",
            true,
            true,
            "Error.txt",
            true);

        Environment.ExitCode = 1;
        throw new Exception("Not enough / too many arguments provided");
    }
    
    await LogAsync(
        "Started - " + argsStringForLogging,
        writeToConsole,
        writeToFile,
        "Started.txt");
    
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

static async Task LogAsync(
    string content,
    bool shouldConsoleWrite,
    bool shouldWriteFile,
    string? filename = null,
    bool isError = false)
{
    if (shouldConsoleWrite)
    {
        var infoOrError = isError ? "ERROR" : "INFO";
        ConsoleHelper.WriteLine($"{infoOrError} - TesseractExe - {content}");
    }

    if (!shouldWriteFile)
    {
        return;
    }
    
    await File.WriteAllTextAsync(
        filename!,
        content + "\n" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
}