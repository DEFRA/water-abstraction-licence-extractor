using System.Globalization;
using System.Text;
using CsvHelper;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
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
                IsLive = licence.IsLiveLicence,
                IsDead = licence.IsDeadLicence,
                IsImpoundment = licence.IsImpoundmentLicence,
                LicenceFoundInList = licence.LicenceFoundInList,
                
                LinkedLicenceNumber = "--",
                ScrapedLinkedLicenceNumber = "--",
                NaldLinkedLicenceNumber = "--",
                LinkedLicenceFilename = "--",
                LinkedLicenceDmsPath = "--",
                LinkedLicenceSectionAndReason = "--",
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
                var sectionAndReason = string.Join("\n", linkedLicence.ContainedIn!
                    .Select(ci =>
                    {
                        if (ci.Source == LinkedLicenceSource.Nald)
                        {
                            if (ci.LinkReason == "ImplicitBackLink")
                            {
                                return $"{ci.Source}-{ci.LinkReason}-{ci.SectionName ?? "UNKNOWN"}";
                            }
                            
                            return $"{ci.Source}-{ci.LinkReason ?? "UNKNOWN"}-{ci.SectionName ?? "UNKNOWN"}";
                        }

                        if (ci.Source == LinkedLicenceSource.OtherDocument)
                        {
                            return $"Document-{ci.SectionName ?? "OtherDocument"}-{ci.LinkReason ?? "UNKNOWN"}";
                        }
                        
                        return $"{ci.Source}-{ci.LinkReason ?? "UNKNOWN"}-{ci.SectionName ?? "UNKNOWN"}" +
                            $"-P{ci.PageNumber ?? -1}-L{ci.LineNumber ?? -1}";
                    }));

                var outputLineCloned = outputLine.Clone();
                
                outputLineCloned.LinkedLicenceNumber = linkedLicence.LicenceNumber;
                outputLineCloned.ScrapedLinkedLicenceNumber = linkedLicence.ScrapedLicenceNumber;
                outputLineCloned.NaldLinkedLicenceNumber = linkedLicence.NaldLicenceNumber;
                outputLineCloned.LinkedLicenceFilename = linkedLicence.Filename;
                outputLineCloned.LinkedLicenceDmsPath = !string.IsNullOrEmpty(linkedLicence.DmsPath)
                    ? $"=HYPERLINK(\"{linkedLicence.DmsPath}\")"
                    : null;
                outputLineCloned.LinkedLicenceSectionAndReason = sectionAndReason;
                outputLineCloned.LinkedLicenceFoundInList = linkedLicence.LicenceFoundInList;
                outputLineCloned.LinkedLicenceIsLive = linkedLicence.IsLiveLicence;
                outputLineCloned.LinkedLicenceIsDead = linkedLicence.IsDeadLicence;
                outputLineCloned.LinkedLicenceIsImpoundment = linkedLicence.IsImpoundmentLicence;
                
                returnList.Add(outputLineCloned);
            }
        }

        return returnList;
    }
}