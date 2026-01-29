using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class GenerateEaLicenceFeaturesCsv
{
    private static readonly NpgsqlDataSourceProvider NpgsqlDataSourceProvider = new(
        KeyConfig.PostgresHost,
        KeyConfig.PostgresPort,
        KeyConfig.PostgresDbName,
        KeyConfig.PostgresUsername,
        KeyConfig.PostgresPassword);

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
            
            var hasMultipleScheduleOfConditions = (bool)licence.NoneSchemaData[TemplateFeatures.MultipleScheduleOfConditions];
            var hasPointsTable = licence.NoneSchemaData.TryGetValue(TemplateFeatures.PointsTable, out var pointTableFeature1)
                && (bool)pointTableFeature1;
            
            var hasMeansPointsTable = licence.NoneSchemaData.TryGetValue(TemplateFeatures.MeansPointsTable, out var pointTableFeature2)
                && (bool)pointTableFeature2;
            
            var hasLimitsPointsTable = licence.NoneSchemaData.TryGetValue(TemplateFeatures.LimitPointsTable, out var pointTableFeature3)
                && (bool)pointTableFeature3;
            
            var usesOcr = (string)licence.NoneSchemaData["ocr"] == "OCR";

            if (usesOcr
                && !hasMultipleScheduleOfConditions
                && !hasPointsTable
                && !hasMeansPointsTable
                && !hasLimitsPointsTable)
            {
                continue;
            }

            returnList.Add(new EALicenceFeaturesCsvLine
            {
                Filename = licence.Filename,
                LicenceNumber = licence.LicenceNumber,
                HasPointTable = hasPointsTable,
                HasLimitsPointTable = hasLimitsPointsTable,
                HasMeansPointTable = hasMeansPointsTable,
                HasMultipleSchedules = hasMultipleScheduleOfConditions
            });
        }
        
        return returnList;
    }
}