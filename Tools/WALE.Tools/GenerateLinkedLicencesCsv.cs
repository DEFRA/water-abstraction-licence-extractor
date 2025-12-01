using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.Services;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class GenerateLinkedLicencesCsv
{
    private static readonly IOutputService OutputService = new DatabaseOutputService(
        new SqlSeverReadService(KeyConfig.SqlConnectionString),
        new SqlSeverWriteService(KeyConfig.SqlConnectionString));
    
    public static async Task GenerateCsvAsync()
    {
        Console.WriteLine("Started generating linked licences csv");

        var data = await GetDataAsync();

        await using var writer = new StreamWriter($"LinkedLicences-{DateTime.Today:yyyyMMdd}.csv");
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(data);
        Console.WriteLine("Finished generating linked licences csv");
    }

    private static async Task<List<LinkedLicencesCsvLine>> GetDataAsync()
    {
        var returnList = new List<LinkedLicencesCsvLine>();
        
        const int processRunId = 670;
        const string scrapedLicenceNumberKey = "scrapedLicenceNumber";
        
        var licences = await OutputService.GetLicencesAsync(processRunId);
        
        foreach (var licence in licences)
        {
            if (licence.LinkedLicences.Length == 0)
            {
                var licenceNumber = licence.NoneSchemaData.TryGetValue(scrapedLicenceNumberKey, out var value)
                    ? value.ToString()
                    : null;
                
                returnList.Add(new LinkedLicencesCsvLine
                {
                    Filename = licence.Filename,
                    LicenceNumber = licence.LicenceNumber,
                    ScrapedLicenceNumber = licenceNumber,
                    NaldLicenceNumber = licence.NaldLicenceNumber,
                    LicenceFoundInList = licence.LicenceFoundInList,
                    LicenceIsLive = licence.IsLiveLicence,
                    LicenceIsDead = licence.IsDeadLicence,
                    LicenceIsImpoundment = licence.IsImpoundmentLicence
                });
                
                continue;
            }
            
            foreach (var linkedLicence in licence.LinkedLicences)
            {
                foreach (var fromSection in linkedLicence.ContainedIn!)
                {
                    var licenceNumber = licence.NoneSchemaData.TryGetValue(scrapedLicenceNumberKey, out var value)
                        ? value.ToString()
                        : null;
                    
                    returnList.Add(new LinkedLicencesCsvLine
                    {
                        Filename = licence.Filename,
                        LicenceNumber = licence.LicenceNumber,
                        ScrapedLicenceNumber = licenceNumber,
                        NaldLicenceNumber = licence.NaldLicenceNumber,
                        LicenceFoundInList = licence.LicenceFoundInList,
                        LicenceIsLive = licence.IsLiveLicence,
                        LicenceIsDead = licence.IsDeadLicence,
                        LicenceIsImpoundment = licence.IsImpoundmentLicence,
                        LinkedLicenceNumber = linkedLicence.LicenceNumber,
                        NaldLinkedLicenceNumber = linkedLicence.NaldLicenceNumber,
                        LinkedLicenceFromSection = fromSection.SectionName,
                        LinkedLicenceLinkReason = fromSection.LinkReason,
                        LinkedLicenceFoundInList = linkedLicence.LicenceFoundInList,
                        LinkedLicenceIsLive = linkedLicence.IsLiveLicence,
                        LinkedLicenceIsDead = linkedLicence.IsDeadLicence,
                        LinkedLicenceIsImpoundment = linkedLicence.IsImpoundmentLicence
                    });
                }
            }
        }

        return returnList;
    }
}