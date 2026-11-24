using System.Globalization;
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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
var (portNumber, writeToConsole, writeToFile) = await SetupAsync(args);

app.MapPost("/setup", (SetupTesseractRequest request) =>
    {
        TesseractService.Instance = GetTesseractService(
            request.BytesMode!.Equals("file", StringComparison.OrdinalIgnoreCase),
            request.PageSegMode,
            request.CacheFolderOrApiUrl!,
            request.OutputFolder!,
            request.TessDataPath!);
        
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

static async Task<(int PortNumber, bool WriteToConsole, bool WriteToFile)> SetupAsync(string[] args)
{
    var configuration = GetConfiguration();

    var writeToConsole = configuration.GetValue<bool?>("writeToConsole") ?? true;
    var writeToFile = configuration.GetValue<bool?>("writeDebugLogs") ?? false;
    
    var argsStringForLogging = string.Join(' ', args);

    if (args.Length != 1)
    {
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