using System.Globalization;
using System.Text;
using CsvHelper;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Output;
using WALE.Tools.Config;
using WALE.Tools.Models;

namespace WALE.Tools._2ndHalf;

public static class GenerateLinkedLicencesCsv
{
    private static readonly HttpClient HttpClient = new()
    {
        BaseAddress = new Uri(KeyConfig.ApiBaseUrl)
    };

    private static readonly IOutputService OutputService = new ApiOutputService(HttpClient);
    
    public static async Task GenerateCsvAsync(int processRunId)
    {
        ConsoleHelper.WriteLine("Started generating linked licences csv");

        var data = await GetDataAsync(processRunId);

        await using var writer = new StreamWriter(
            $"LinkedLicences-{DateTime.Today:yyyyMMdd}.csv",
            false,
            Encoding.Unicode);
        await using var csv = new CsvWriter(writer, new CultureInfo("en-GB"));

        await csv.WriteRecordsAsync(data);
        ConsoleHelper.WriteLine("Finished generating linked licences csv");
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
            
            var licenceNumber = licence.NoneSchemaData.TryGetValue(scrapedLicenceNumberKey, out var value)
                ? value?.ToString()
                : null;

            var isFound = licence.NaldStatus == NaldLicenceStatus.Live
                || licence.NaldStatus == NaldLicenceStatus.Dead
                || licence.LicenceType == LicenceType.Impoundment;
            
            var outputLine = new LinkedLicencesCsvLine
            {
                Filename = licence.Filename,
                DmsPath = !string.IsNullOrEmpty(licence.DmsPath) ? $"=HYPERLINK(\"{licence.DmsPath}\")" : null,
                FileId = licence.DmsFileId,
                PermitNumber = licence.PermitNumber,
                LicenceNumber = licence.LicenceNumber?.Value,
                ScrapedLicenceNumber = licenceNumber,
                NaldLicenceNumber = licence.NaldLicenceNumber,
                DateOfIssue = licence.LicenceVersion.IssueDate?.ToString("dd/MM/yyyy"),
                IssuedBy = licence.LicenceVersion.Issuer,
                HasInlicenceAggregates = licence.AbstractionLimits
                    .Aggregates?.Any(agg => agg.PrimaryType == PrimaryType.InLicence) ?? false,
                HasLicenceToLicenceAggregates = licence.AbstractionLimits
                    .Aggregates?.Any(agg => agg.PrimaryType == PrimaryType.LicenceToLicence) ?? false,
                IsLive = licence.NaldStatus == NaldLicenceStatus.Live,
                IsDead = licence.NaldStatus == NaldLicenceStatus.Dead,
                IsImpoundment = licence.LicenceType == LicenceType.Impoundment,
                LicenceFoundInList = isFound,
                LinkedLicenceNumber = "--",
                ScrapedLinkedLicenceNumber = "--",
                LinkedLicenceFilename = "--",
                LinkedLicenceDmsPath = "--",
                LinkedLicenceDocumentIncoming = "--",
                LinkedLicenceDocumentOutgoing = "--",
                LinkedLicenceNaldIncoming = "--",
                LinkedLicenceNaldOutgoing = "--",
                LinkedLicenceFoundInList = null,
                LinkedLicenceIsLive = null,
                LinkedLicenceIsDead = null,
                LinkedLicenceIsImpoundment = null
            };
            
            if (licence.LinkedLicences.Length == 0)
            {
                returnList.Add(outputLine);
                continue;
            }
            
            foreach (var linkedLicence in licence.LinkedLicences)
            {
                var outputLineCloned = outputLine.Clone();
                
                var linkedLicenceIsFound = linkedLicence.NaldStatus == NaldLicenceStatus.Live
                    || linkedLicence.NaldStatus == NaldLicenceStatus.Dead
                    || linkedLicence.LicenceType == LicenceType.Impoundment;
                
                outputLineCloned.LinkedLicenceNumber = linkedLicence.LicenceNumber;
                outputLineCloned.ScrapedLinkedLicenceNumber = linkedLicence.RawScrapedLicenceNumber;
                outputLineCloned.LinkedLicenceFilename = linkedLicence.Filename;
                outputLineCloned.LinkedLicenceDmsPath = !string.IsNullOrEmpty(linkedLicence.DmsPath)
                    ? $"=HYPERLINK(\"{linkedLicence.DmsPath}\")"
                    : null;

                outputLineCloned.LinkedLicenceDocumentIncoming = GetContainedInText(linkedLicence.ContainedIn!
                        .Where(ci => ci.Source == LinkedLicenceSource.OtherDocument)
                        .ToArray());
                
                outputLineCloned.LinkedLicenceDocumentOutgoing = GetContainedInText(linkedLicence.ContainedIn!
                    .Where(ci => ci.Source == LinkedLicenceSource.Document)
                    .ToArray());
                
                outputLineCloned.LinkedLicenceNaldIncoming = GetContainedInText(linkedLicence.ContainedIn!
                    .Where(ci => ci is { Source: LinkedLicenceSource.Nald, LinkReason: "Incoming" })
                    .ToArray());
                
                outputLineCloned.LinkedLicenceNaldOutgoing = GetContainedInText(linkedLicence.ContainedIn!
                    .Where(ci => ci is { Source: LinkedLicenceSource.Nald, LinkReason: "Outgoing" })
                    .ToArray());
                
                outputLineCloned.LinkedLicenceFoundInList = linkedLicenceIsFound;
                outputLineCloned.LinkedLicenceIsLive = linkedLicence.NaldStatus == NaldLicenceStatus.Live;
                outputLineCloned.LinkedLicenceIsDead = linkedLicence.NaldStatus == NaldLicenceStatus.Dead;
                outputLineCloned.LinkedLicenceIsImpoundment = linkedLicence.LicenceType == LicenceType.Impoundment;
                
                returnList.Add(outputLineCloned);
            }
        }

        return returnList;
    }

    private static string GetContainedInText(LinkedLicenceSection[] containedIn)
    {
        if (containedIn.Length == 0)
        {
            return "--";
        }
        
        return string.Join("; ", containedIn
            .Select(ci =>
            {
                if (ci.Source == LinkedLicenceSource.Nald)
                {
                    return $"{ci.Source}-{ci.LinkReason ?? "UNKNOWN"}-{ci.SectionName ?? "UNKNOWN"}";
                }

                if (ci.Source == LinkedLicenceSource.OtherDocument)
                {
                    return $"Document-Incoming-{ci.LinkReason ?? "UNKNOWN"}";
                }
                        
                return $"{ci.Source}-Outgoing-{ci.LinkReason ?? "UNKNOWN"}-{ci.SectionName ?? "UNKNOWN"}" +
                       $"-P{ci.PageNumber ?? -1}-L{ci.LineNumber ?? -1}";
            }));
    }
}