using System.Text.Json;
using WALE.ProcessFile.Database.Services;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums.OutputSchema;
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

var config = new ConfigurationBuilder();
config.AddJsonFile("appsettings.json");
config.AddJsonFile("appsettings.Development.json");

var outputService = GetOutputService(config.Build());

app.MapGet("/list", async () =>
{
    var processRuns = await outputService.GetProcessRunsAsync();
    var processRunId = processRuns
        .OrderByDescending(processRun => processRun.ProcessRunId)
        .FirstOrDefault()?.ProcessRunId ?? -1;

    var completeNumber = 1;
    var fileNumber = 1;
    
    var licences = await outputService.GetLicencesAsync(processRunId);
    var licenceSets = await outputService.GetLicenceSetsAsync(processRunId, licences);
    
    var outputLines = licences
        .Where(licence => licence.Status == LicenceStatus.Ok)
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
        new ProcessRun(), // Not used
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
    
    return Results.File(data!, "image/jpeg");
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

static IOutputService GetOutputService(IConfiguration configuration)
{
    var sqlConnectionString = configuration.GetValue<string>("SqlConnectionString")!;

    var sqlAddService = new SqlSeverAddServiceService(sqlConnectionString);
    var sqlReadService = new SqlSeverReadServiceService(sqlConnectionString);
    var outputService = (IOutputService)new DatabaseOutputService(sqlReadService, sqlAddService);
    
    return outputService;
}