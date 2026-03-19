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
            $"LinkedLicences-{DateTime.Now:yyyyMMdd-hhmm}.csv",
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
                || licence.NaldStatus == NaldLicenceStatus.Lapsed
                || licence.NaldStatus == NaldLicenceStatus.Expired
                || licence.NaldStatus == NaldLicenceStatus.Revoked                    
                || licence.LicenceType == LicenceType.Impoundment;
            
            var arepEuicCode = licence.NoneSchemaData.TryGetValue("ArepEuicCode", out var value2)
                ? value2?.ToString()
                : null;
            
            var outputLine = new LinkedLicencesCsvLine
            {
                Filename = licence.Filename,
                DmsPath = licence.DmsPath,
                FileId = licence.DmsFileId,
                FileIdStatus = licence.LicenceVersion.DmsFileIdStatus,
                FileIdStatusChangeDate = licence.LicenceVersion.DmsFileIdStatusDateUtc?.ToString("dd/MM/yyyy"),
                IssueNumber = licence.LicenceVersion.NaldIssueNumber,
                IncrementNumber = licence.LicenceVersion.NaldIncrementNumber,
                PermitNumber = licence.DmsPermitNumber,
                LicenceNumber = licence.LicenceNumber?.Value,
                ScrapedLicenceNumber = licenceNumber,
                DateOfIssue = licence.LicenceVersion.IssueDate?.ToString("dd/MM/yyyy"),
                IssuedBy = licence.LicenceVersion.Issuer,
                HasInlicenceAggregates = licence.AbstractionLimits
                    .Aggregates?.Any(agg => agg.PrimaryType == PrimaryType.InLicence) ?? false,
                HasLicenceToLicenceAggregates = licence.AbstractionLimits
                    .Aggregates?.Any(agg => agg.PrimaryType == PrimaryType.LicenceToLicence) ?? false,
                IsLive = licence.NaldStatus == NaldLicenceStatus.Live,
                IsLapsed = licence.NaldStatus == NaldLicenceStatus.Lapsed,
                IsRevoked = licence.NaldStatus == NaldLicenceStatus.Revoked,
                IsExpired = licence.NaldStatus == NaldLicenceStatus.Expired,
                IsImpoundment = licence.LicenceType == LicenceType.Impoundment,
                LicenceFoundInList = isFound,
                ArepEuicCode = arepEuicCode
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
                    || linkedLicence.NaldStatus == NaldLicenceStatus.Lapsed
                    || linkedLicence.NaldStatus == NaldLicenceStatus.Expired
                    || linkedLicence.NaldStatus == NaldLicenceStatus.Revoked    
                    || linkedLicence.LicenceType == LicenceType.Impoundment;

                outputLineCloned.LinkedLicenceNumber = linkedLicence.LicenceNumber;
                outputLineCloned.ScrapedLinkedLicenceNumber = linkedLicence.RawScrapedLicenceNumber;
                outputLineCloned.LinkedLicenceFilename = linkedLicence.Filename;
                outputLineCloned.LinkedLicenceDmsPath = linkedLicence.DmsPath!;

                outputLineCloned.LinkedLicenceDocumentIncoming = GetContainedInText(linkedLicence.ContainedIn!
                    .Where(ci => ci.Source == LinkedLicenceSource.OtherDocument)
                    .ToArray(), linkedLicence.LicenceType);
                
                outputLineCloned.LinkedLicenceDocumentOutgoing = GetContainedInText(linkedLicence.ContainedIn!
                    .Where(ci => ci.Source == LinkedLicenceSource.Document)
                    .ToArray(), linkedLicence.LicenceType);
                
                outputLineCloned.LinkedLicenceNaldIncoming = GetContainedInText(linkedLicence.ContainedIn!
                    .Where(ci => ci is { Source: LinkedLicenceSource.Nald, Direction: LinkedLicenceDirection.Incoming })
                    .ToArray(), linkedLicence.LicenceType);
                
                outputLineCloned.LinkedLicenceNaldOutgoing = GetContainedInText(linkedLicence.ContainedIn!
                    .Where(ci => ci is { Source: LinkedLicenceSource.Nald, Direction: LinkedLicenceDirection.Outgoing })
                    .ToArray(), linkedLicence.LicenceType);
                
                outputLineCloned.LinkedLicenceFoundInList = linkedLicenceIsFound;
                outputLineCloned.LinkedLicenceIsLive = linkedLicence.NaldStatus == NaldLicenceStatus.Live;
                outputLineCloned.LinkedLicenceIsLapsed = linkedLicence.NaldStatus == NaldLicenceStatus.Lapsed;
                outputLineCloned.LinkedLicenceIsExpired = linkedLicence.NaldStatus == NaldLicenceStatus.Expired;
                outputLineCloned.LinkedLicenceIsRevoked = linkedLicence.NaldStatus == NaldLicenceStatus.Revoked;
                outputLineCloned.LinkedLicenceIsImpoundment = linkedLicence.LicenceType == LicenceType.Impoundment;
                
                returnList.Add(outputLineCloned);
            }
        }

        return returnList;
    }

    private static string? GetContainedInText(
        LinkedLicenceSection[] containedIn,
        LicenceType licenceType)
    {
        if (containedIn.Length == 0)
        {
            return null;
        }
        
        return string.Join("; ", containedIn
            .Select(ci =>
            {
                var licenceTypeSuffix = string.Empty;
                if (licenceType != LicenceType.Abstraction)
                {
                    licenceTypeSuffix = $" ({licenceType.ToString()})";
                }
                
                if (ci.Source == LinkedLicenceSource.Nald)
                {
                    return $"{ci.Source}-{ci.LinkReason ?? "UNKNOWN"}-{ci.SectionName ?? "UNKNOWN"}{licenceTypeSuffix}";
                }

                if (ci.Source == LinkedLicenceSource.OtherDocument)
                {
                    return $"Document-Incoming-{ci.LinkReason ?? "UNKNOWN"}{licenceTypeSuffix}";
                }
                        
                return $"{ci.Source}-Outgoing-{ci.LinkReason ?? "UNKNOWN"}-{ci.SectionName ?? "UNKNOWN"}" +
                       $"-P{ci.PageNumber ?? -1}-L{ci.LineNumber ?? -1}{licenceTypeSuffix}";
            }));
    }
}