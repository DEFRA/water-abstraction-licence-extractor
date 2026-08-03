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

public static class GenerateUnknownSectionLinkedLicencesCsv
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri(KeyConfig.ApiBaseUrl)
    };

    private static readonly IOutputService OutputService = new ApiOutputService(HttpClient);
    
    private static readonly IAbstractionLicenceOutputService AbsLicenceOutputService =
        new ApiAbstractionLicenceOutputService(HttpClient);
    
    public static async Task GenerateCsvAsync(int processRunId)
    {
        ConsoleHelper.WriteLine("Started generating unknown section linked licences csv");

        var data = await GetDataAsync(processRunId);

        await using var writer = new StreamWriter($"UnknownSectionLinkedLicences-{DateTime.Today:yyyyMMdd}.csv");
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));

        await csv.WriteRecordsAsync(data);
        ConsoleHelper.WriteLine("Finished generating unknown section linked licences csv");
    }

    private static async Task<List<UnknownSectionLinkedLicencesCsvLine>> GetDataAsync(int processRunId)
    {
        var returnList = new List<UnknownSectionLinkedLicencesCsvLine>();
        const string scrapedLicenceNumberKey = "scrapedLicenceNumber";
        
        var licences = await AbsLicenceOutputService.GetLicencesAsync(processRunId, 0, int.MaxValue);
        
        foreach (var licence in licences)
        {
            if (string.IsNullOrEmpty(licence.Filename))
            {
                continue;
            }

            var unknownLinkedLicences = licence.NoneSchemaData
                .Where(kvp => kvp.Key.StartsWith("AdditionalLinkedLicence:"))
                .Select(kvp =>
                {
                    var json = kvp.Value?.ToString()!;
                    return JsonSerializer.Deserialize<LinkedLicence>(json, JsonHelper.GetSerializerOptions());
                })
                .ToList();
            
            if (unknownLinkedLicences.Count == 0)
            {
                continue;
            }
            
            foreach (var linkedLicence in unknownLinkedLicences)
            {
                var scrapedLicenceNumber = licence.NoneSchemaData.TryGetValue(scrapedLicenceNumberKey, out var value)
                    ? value?.ToString()
                    : null;
                
                var isFound = licence.NaldStatus == NaldLicenceStatus.Live
                    || licence.NaldStatus == NaldLicenceStatus.Lapsed
                    || licence.NaldStatus == NaldLicenceStatus.Expired
                    || licence.NaldStatus == NaldLicenceStatus.Revoked                    
                    || licence.LicenceType == LicenceType.Impoundment;
                
                returnList.Add(new UnknownSectionLinkedLicencesCsvLine
                {
                    Filename = licence.Filename,
                    LicenceNumber = licence.LicenceNumber?.Value,
                    ScrapedLicenceNumber = scrapedLicenceNumber,
                    LicenceFoundInList = isFound,
                    LicenceIsLive = licence.NaldStatus == NaldLicenceStatus.Live,
                    LicenceIsDead = licence.NaldStatus is
                        NaldLicenceStatus.Lapsed
                        or NaldLicenceStatus.Revoked
                        or NaldLicenceStatus.Expired,
                    LicenceIsImpoundment = licence.LicenceType == LicenceType.Impoundment,
                    LinkedLicenceNumber = linkedLicence!.LicenceNumber,
                    PageNumber = -1
                });
            }
        }

        return returnList;
    }
}