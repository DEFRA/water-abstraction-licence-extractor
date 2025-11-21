using System.Collections;
using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Database.Services;
using WALE.ProcessFile.Models.Interfaces;
using WALE.ProcessFile.Services.Helpers;
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

        await csv.WriteRecordsAsync((IEnumerable)data);
        Console.WriteLine("Finished generating linked licences csv");
    }

    static async Task<List<LinkedLicencesCsvLine>> GetDataAsync()
    {
        var returnList = new List<LinkedLicencesCsvLine>();
        
        var pdfFilePaths = FileHelper
            .GetFiles(KeyConfig.PdfFolder)
            .Select(FileHelper.GetFilenameWithoutExtension)
            .OrderBy(fileName => fileName).ToList();

        foreach (var pdfFilePath in pdfFilePaths)
        {
            var licence = await OutputService.GetLicenceAsync(pdfFilePath!);

            if (licence == null)
            {
                Console.WriteLine($"Error - {pdfFilePath} not found");
                continue;
            }

            if (licence.LinkedLicences.Length == 0)
            {
                returnList.Add(new LinkedLicencesCsvLine
                {
                    Filename = licence.Filename,
                    LicenceNumber = licence.LicenceNumber,
                    ScrapedLicenceNumber = (string)licence.NoneSchemaData["x"],
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
                foreach (var fromSection in linkedLicence.FromSection!)
                {
                    returnList.Add(new LinkedLicencesCsvLine
                    {
                        Filename = licence.Filename,
                        LicenceNumber = licence.LicenceNumber,
                        ScrapedLicenceNumber = (string)licence.NoneSchemaData["x"],
                        NaldLicenceNumber = licence.NaldLicenceNumber,
                        LicenceFoundInList = licence.LicenceFoundInList,
                        LicenceIsLive = licence.IsLiveLicence,
                        LicenceIsDead = licence.IsDeadLicence,
                        LicenceIsImpoundment = licence.IsImpoundmentLicence,
                        LinkedLicenceNumber = linkedLicence.LicenceNumber,
                        NaldLinkedLicenceNumber = linkedLicence.NaldLicenceNumber,
                        LinkedLicenceFromSection = fromSection,
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