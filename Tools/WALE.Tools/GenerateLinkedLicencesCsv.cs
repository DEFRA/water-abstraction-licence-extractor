using System.Globalization;
using System.Text;
using System.Web;
using CsvHelper;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Database.PostgreSQL.Services;
using WALE.ProcessFile.Services.Services;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools;

public static class GenerateLinkedLicencesCsv
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
        Console.WriteLine("Started generating linked licences csv");

        var data = await GetDataAsync(processRunId);

        await using var writer = new StreamWriter(
            $"LinkedLicences-{DateTime.Today:yyyyMMdd}.csv",
            false,
            Encoding.Unicode);
        await using var csv = new CsvWriter(writer, CultureInfo.InvariantCulture);

        await csv.WriteRecordsAsync(data);
        Console.WriteLine("Finished generating linked licences csv");
    }

    private static async Task<List<LinkedLicencesCsvLine>> GetDataAsync(int processRunId)
    {
        var returnList = new List<LinkedLicencesCsvLine>();
        const string scrapedLicenceNumberKey = "scrapedLicenceNumber";
        
        var licences = await OutputService.GetLicencesAsync(processRunId);
        
        foreach (var licence in licences)
        {
            if (string.IsNullOrEmpty(licence.Filename))
            {
                continue;
            }
            
            if (licence.LinkedLicences.Length == 0)
            {
                var licenceNumber = licence.NoneSchemaData.TryGetValue(scrapedLicenceNumberKey, out var value)
                    ? value.ToString()
                    : null;
                
                returnList.Add(new LinkedLicencesCsvLine
                {
                    Filename = licence.Filename,
                    DmsPath = !string.IsNullOrEmpty(licence.DmsPath) ? $"=HYPERLINK(\"{licence.DmsPath}\")" : null,
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
                var fromSections = string.Join(';', linkedLicence.ContainedIn!.Select(ci => ci.SectionName));
                var linkReasons = string.Join(';', linkedLicence.ContainedIn!.Select(ci => ci.LinkReason));
                
                var licenceNumber = licence.NoneSchemaData.TryGetValue(scrapedLicenceNumberKey, out var value)
                    ? value.ToString()
                    : null;

                returnList.Add(new LinkedLicencesCsvLine
                {
                    Filename = licence.Filename,
                    DmsPath = !string.IsNullOrEmpty(licence.DmsPath) ? $"=HYPERLINK(\"{licence.DmsPath}\")" : null,
                    LicenceNumber = licence.LicenceNumber,
                    ScrapedLicenceNumber = licenceNumber,
                    NaldLicenceNumber = licence.NaldLicenceNumber,
                    LicenceFoundInList = licence.LicenceFoundInList,
                    LicenceIsLive = licence.IsLiveLicence,
                    LicenceIsDead = licence.IsDeadLicence,
                    LicenceIsImpoundment = licence.IsImpoundmentLicence,
                    LinkedLicenceNumber = linkedLicence.LicenceNumber,
                    LinkedLicenceFilename = linkedLicence.Filename,
                    LinkedLicenceDmsPath = !string.IsNullOrEmpty(linkedLicence.DmsPath) ? $"=HYPERLINK(\"{linkedLicence.DmsPath}\")" : null,
                    LinkedLicenceFromSection = fromSections,
                    LinkedLicenceLinkReason = linkReasons,
                    LinkedLicenceFoundInList = linkedLicence.LicenceFoundInList,
                    LinkedLicenceIsLive = linkedLicence.IsLiveLicence,
                    LinkedLicenceIsDead = linkedLicence.IsDeadLicence,
                    LinkedLicenceIsImpoundment = linkedLicence.IsImpoundmentLicence
                });
            }
        }

        return returnList;
    }
}