using System.Text.Json;
using WALE.ProcessFile.Database.Services;
using WALE.ProcessFile.Models;
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

app.MapGet("/list", async () =>
    {
        var outputService = GetOutputService();
        
        var processRuns = await outputService.GetProcessRunsAsync();
        var processRunId = processRuns
            .OrderByDescending(processRun => processRun.ProcessRunId)
            .FirstOrDefault()?.ProcessRunId ?? -1;

        var completeNumber = 1;
        var fileNumber = 1;
        
        var licences = await outputService.GetLicencesAsync(processRunId);
        var licenceSets = await outputService.GetLicenceSetsAsync(processRunId);
        
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
            new ProcessRun(), // Not used
            false);

        var serializedData = JsonSerializer.Serialize(
            listData,
            JsonHelper.GetSerializerOptions());
        
        return $"var data = {serializedData};";
    })
    .WithName("GetLicences");

app.Run();
return;

static IOutputService GetOutputService()
{
    var sqlConnectionString = Environment.GetEnvironmentVariable("SqlConnectionString")!;

    var sqlAddService = new SqlSeverAddServiceService(sqlConnectionString);
    var sqlReadService = new SqlSeverReadServiceService(sqlConnectionString);
    var outputService = (IOutputService)new DatabaseOutputService(sqlReadService, sqlAddService);
    
    return outputService;
}