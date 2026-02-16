using System.Globalization;
using Microsoft.Extensions.Configuration;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.Tesseract;

try
{
    var configuration = GetConfiguration();

    var writeToConsole = configuration.GetValue<bool?>("writeToConsole") ?? true;
    var writeToFile = configuration.GetValue<bool>("writeDebugLogs");
    
    var argsStringForLogging = string.Join(' ', args);
    if (args.Length >= 16)
    {
        var postgresPasswordTemp = args[15];
        argsStringForLogging = argsStringForLogging.Replace(postgresPasswordTemp, "*****");
    }

    await WriteLogFileIfDebugModeAsync(
        "Started.txt",
        $"{typeof(Program).Assembly.GetName().Name} started - " + argsStringForLogging,
        writeToConsole,
        writeToFile);

    if (args.Length < 16)
    {
        throw new Exception("Not enough arguments provided");
    }

    var pageSegMode = Enum.Parse<WALE.ProcessFile.Core.Enums.PageSegMode>(args[0]);
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
    
    var tesseractService = GetTesseractService(
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

    var textLines = await tesseractService.ProcessAsync(
        "PdfPig",
        pageNumber,
        imageNumber,
        isPageScreenshot,
        imageReference,
        pdfFilepath,
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
         "ERROR - " + ex,
        true,
        true);

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

static InternalTesseractOcrDataExtractorService GetTesseractService(
    bool isFileMode,
    WALE.ProcessFile.Core.Enums.PageSegMode pageSegMode,
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
    
    ICacheService cacheService;
    IOutputService outputService;
    
    if (isFileMode)
    {
        outputService = new FileSystemOutputService(outputFolder);
        cacheService = new FileSystemCacheService(cacheFolder);
    }
    else
    {
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

        outputService = new DatabaseOutputService(databaseReadService, databaseAddService);
        cacheService = new DatabaseCacheService(
            databaseReadService,
            databaseAddService,
            postgresHost,
            postgresPort,
            postgresDatabaseName,
            postgresUsername,
            postgresPassword);
    }
    
    return new InternalTesseractOcrDataExtractorService(
        outputService,
        cacheService,
        tessDataPath,
        pageSegMode);
}

static async Task WriteLogFileIfDebugModeAsync(string filename, string content, bool shouldConsoleWrite, bool shouldWriteFile)
{
    if (shouldConsoleWrite)
    {
        Console.WriteLine(content);
    }

    if (!shouldWriteFile)
    {
        return;
    }
    
    await File.WriteAllTextAsync(
        filename,
        content + "\n" + DateTime.Now.ToString(CultureInfo.InvariantCulture));
}