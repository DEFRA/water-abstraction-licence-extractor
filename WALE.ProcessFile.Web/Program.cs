using System.Text;
using System.Text.Json;
using WALE.ProcessFile.Database.Services;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Services;

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

var configBuilder = new ConfigurationBuilder();
configBuilder.AddJsonFile("appsettings.json");
configBuilder.AddJsonFile("appsettings.Development.json");

var config = configBuilder.Build();

var cacheService = GetCacheService(config);
var outputService = GetOutputService(config);
var indexLink = "list.html?showAll=true&processRunId=";

app.MapGet("/process-run", async () =>
{
    var processRuns = await outputService.GetProcessRunsAsync();

    var listData = new List<string>();
    
    foreach (var processRun in processRuns.OrderByDescending(pr => pr.ProcessRunId))
    {
        listData.Add($"<li><a href='{indexLink}{processRun.ProcessRunId}'>{processRun.ProcessRunId} - {processRun.StartDateTimeUtc}</a> - {processRun.Description} ({processRun.NumberOfFiles} files)</li>");
    }

    var serializedData = JsonSerializer.Serialize(
        listData,
        JsonHelper.GetSerializerOptions());
    
    return $"var data = {serializedData};";
}).WithName("ChooseProcessRun");

app.MapGet("/list", async (int processRunId) =>
{
    var licencesTask = outputService.GetLicencesAsync(processRunId);
    var licenceSets = await outputService.GetLicenceSetsAsync(processRunId);
    var licences = await licencesTask;
    
    var completeNumber = 1;
    var fileNumber = 1;
    
    var outputLines = licences
        .Select(licence => JsOutputHelper.ToOutputLine(
            licence,
            DateTime.Now,
            completeNumber++,
            fileNumber++,
            licenceSets))
        .ToList();

    var listData = await JsOutputHelper.SaveListDataAsync(
        outputLines,
        string.Empty,// Not used
        outputService,// Not used
        false, // Not used
        new ProcessRun
        {
            ProcessRunId = processRunId
        }, // Not used
        false);

    var serializedData = JsonSerializer.Serialize(
        listData,
        JsonHelper.GetSerializerOptions());
    
    return $"var data = {serializedData};";
}).WithName("GetLicences");

app.MapGet("/thumbnail", async (string filename) =>
{
    var parts = filename.Split('/');
    var fileName1 = parts[0];
    var serviceName = parts[1];

    var pageNumberStr = parts.Last()
        .Replace("page-", string.Empty)
        .Replace(".jpg", string.Empty);
        
    var pageNumber = int.Parse(pageNumberStr);
    var data = await outputService.GetPageScreenshotDataAsync(
        pageNumber,
        serviceName,
        fileName1);

    if (data == null)
    {
        throw new Exception($"Cannot find screenshot for {fileName1} - {serviceName} - {pageNumber}");
    }
    
    return Results.File(data, "image/jpeg");
}).WithName("GetThumbnail");

app.MapGet("/image", async (string filename) =>
{
    var parts = filename.Split('/');
    var fileName1 = parts[0];
    var serviceName = parts[1];

    var pageNumberStr = parts.Last()
        .Replace("page-", string.Empty)
        .Replace(".jpg", string.Empty);
        
    var pageNumber = int.Parse(pageNumberStr);
    var data = await outputService.GetPageScreenshotDataAsync(
        pageNumber,
        serviceName,
        fileName1);
    
    return Results.File(data!, "image/jpeg");
}).WithName("GetImage");

app.MapGet("/page-images", async (string filename, int pageNumber) =>
{
    var pageImages = await cacheService.GetImagesAsync(new OcrServiceImageDataCacheRequest
    {
        PageNumber = pageNumber,
        Filepath = filename,
        NoOcrServiceName = PdfDataExtractorService.Name
    });

    var htmlSb = new StringBuilder();
    htmlSb.AppendLine("<html><body>");

    var pageImagesUnique = pageImages
        .GroupBy(pi => pi.imageNumber)
        .Select(pi => pi.Last())
        .OrderBy(pi => pi.imageNumber);
    
    foreach (var pageImage in pageImagesUnique)
    {
        htmlSb.AppendLine($"<img src='/partial-page-image?filename={filename}&extension={pageImage.extension}&pageNumber={pageNumber}&imageNumber={pageImage.imageNumber}' /><hr /><br />");
    }
    
    htmlSb.Append("</body></html>");
    return Results.Text(htmlSb.ToString(), "text/html");
}).WithName("GetPageImages");

app.MapGet("/partial-page-image", async (string filename, string extension, int pageNumber, int imageNumber) =>
{
    var bytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
    {
        PageNumber = pageNumber,
        ImageNumber = imageNumber,
        Filepath = filename,
        NoOcrServiceName = PdfDataExtractorService.Name,
        Extension = extension
    });

    if (bytes == null)
    {
        return Results.NotFound();
    }
    
    return Results.File(bytes, "image/jpeg");
}).WithName("GetPartialPageImage");

app.MapGet("/internal", async (string filename) =>
{
    var serializedData = JsonSerializer.Serialize(
        await outputService.GetMatchesResult(filename),
        JsonHelper.GetSerializerOptions());
    
    return $"var data = {serializedData};";
}).WithName("GetInternal");

app.MapGet("/licence", async (string filename) =>
{
    var serializedData = JsonSerializer.Serialize(
        await outputService.GetLicenceAsync(filename),
        JsonHelper.GetSerializerOptions());
    
    return $"var data2 = {serializedData};";
}).WithName("GetLicence");

app.MapGet("/licenceSets", async (string filename) =>
{
    var serializedData = JsonSerializer.Serialize(
        await outputService.GetLicenceSetsAsync(filename),
        JsonHelper.GetSerializerOptions());
    
    return $"var licenceSets = {serializedData};";
}).WithName("GetLicenceSets");

app.Run();
return;

static ICacheService GetCacheService(IConfiguration configuration)
{
    var sqlConnectionString = configuration.GetValue<string>("SqlConnectionString")!;

    var sqlAddService = new SqlSeverAddServiceService(sqlConnectionString);
    var sqlReadService = new SqlSeverReadServiceService(sqlConnectionString);
    var outputService = (ICacheService)new DatabaseCacheService(sqlReadService, sqlAddService);
    
    return outputService;
}

static IOutputService GetOutputService(IConfiguration configuration)
{
    var sqlConnectionString = configuration.GetValue<string>("SqlConnectionString")!;

    var sqlAddService = new SqlSeverAddServiceService(sqlConnectionString);
    var sqlReadService = new SqlSeverReadServiceService(sqlConnectionString);
    var outputService = (IOutputService)new DatabaseOutputService(sqlReadService, sqlAddService);
    
    return outputService;
}