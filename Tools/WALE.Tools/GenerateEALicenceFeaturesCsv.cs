using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class GenerateEaLicenceFeaturesCsv
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider = new(KeyConfig.PostgresConnectionString);

    private static readonly IOutputService OutputService = new DatabaseOutputService(
        new PostgresReadService(NpgsqlDataSourceProvider),
        new PostgresWriteService(NpgsqlDataSourceProvider));
    
    public static async Task GenerateCsvAsync(int processRunId)
    {
        Console.WriteLine("Started generating EA licence features csv");

        var data = await GetDataAsync(processRunId);

        await using var writer = new StreamWriter($"EaLicenceFeatures-{DateTime.Today:yyyyMMdd}.csv");
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(data);
        Console.WriteLine("Finished generating EA licence features csv");
    }

    private static async Task<List<EALicenceFeaturesCsvLine>> GetDataAsync(int processRunId)
    {
        var returnList = new List<EALicenceFeaturesCsvLine>();
        
        var licences = await OutputService.GetLicencesAsync(processRunId);
        
        foreach (var licence in licences)
        {
            if (string.IsNullOrEmpty(licence.Filename))
            {
                continue;
            }

            if (licence.Filename.Contains("NE0260034052"))
            {
                
            }
            
            var hasMultipleScheduleOfConditions = (bool)licence.NoneSchemaData["Features:MultipleScheduleOfConditions"];
            var hasPointTable = licence.NoneSchemaData.TryGetValue("Features:PointTable", out var pointTableFeature)
                && (bool)pointTableFeature;
            
            var usesOcr = (string)licence.NoneSchemaData["ocr"] == "OCR";

            if (usesOcr && !hasMultipleScheduleOfConditions && !hasPointTable)
            {
                continue;
            }

            if (usesOcr)
            {
                
            }
            
            returnList.Add(new EALicenceFeaturesCsvLine
            {
                Filename = licence.Filename,
                LicenceNumber = licence.LicenceNumber,
                HasPointTable = hasPointTable,
                HasMultipleSchedules = hasMultipleScheduleOfConditions,
            });
        }
        
        return returnList;
    }
}