using System.Collections;
using System.Globalization;
using System.Text.Json;
using CsvHelper;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Services.Output;
using WALE.Tools.Config;
using WALE.Tools.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Services.Output.AbstractionLicence;

namespace WALE.Tools._2ndHalf;

public static class GenerateAggregatesCsvForTesting
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri(KeyConfig.ApiBaseUrl)
    };

    private static readonly IOutputService OutputService = new ApiOutputService(HttpClient);

    private static readonly IAbstractionLicenceOutputService AbsLicenceOutputService =
        new ApiAbstractionLicenceOutputService(HttpClient);
    
    public static async Task<int> GenerateCsvForTestingAsync(int processRunId)
    {
        ConsoleHelper.WriteLine("Started generating aggregates csv");
        
        var data = await GetDataAsync(processRunId);

        await using var writer = new StreamWriter($"Aggregates-{DateTime.Today:yyyyMMdd}.csv");
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));

        await csv.WriteRecordsAsync((IEnumerable)data);
        
        ConsoleHelper.WriteLine("Finished generating aggregates csv");
        return 1;
    }
    
    static async Task<List<AggregatesCsvLine>> GetDataAsync(int processRunId)
    {
        var returnList = new List<AggregatesCsvLine>();
        
        var licences = new List<Licence>();
        var loopLicences = new List<Licence>();
        
        const int licencesToTake = 10;
        var first = true;
        var loopIdx = 0;
        
        while (first || loopLicences.Count == licencesToTake)
        {
            first = false;
            var startAt = loopIdx++ * licencesToTake;
            
            loopLicences = await AbsLicenceOutputService.GetLicencesAsync(processRunId, startAt, licencesToTake);
            licences.AddRange(loopLicences);
        }

        foreach (var licence in licences)
        {
            if (string.IsNullOrEmpty(licence.Filename))
            {
                continue;
            }
            
            returnList.Add(new()
            {
                Filename = licence.Filename,
                LicenceNumber = licence.LicenceNumber!.Value,
                HasInLicenceAggregate = licence.AbstractionLimits
                    .Aggregates?.Any(agg => agg.PrimaryType == PrimaryType.InLicence) ?? false,
                HasLicenceToLicenceAggregate = licence.AbstractionLimits
                    .Aggregates?.Any(agg => agg.PrimaryType == PrimaryType.LicenceToLicence) ?? false,
                AggregateData = JsonSerializer.Serialize(licence.AbstractionLimits.Aggregates, JsonHelper.GetSerializerOptions()),
                IndividualLimits = JsonSerializer.Serialize(licence.AbstractionLimits.Individual, JsonHelper.GetSerializerOptions()),
                //Data = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions()),
                LinkedLicences = JsonSerializer.Serialize(licence.LinkedLicences, JsonHelper.GetSerializerOptions())
            });
        }

        return returnList;
    }
}