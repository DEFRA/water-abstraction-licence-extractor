using WALE.ProcessFile.Services.Output;
using WALE.Tools.Config;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Interfaces;
using WRADI.DocumentType.AbstractionLicence.Services;
using WRADI.Services.Cache.AbstractionLicence;
using WRADI.Services.Output.AbstractionLicence;

namespace WALE.Tools._2ndHalf;

public static class PurposeMapperSinglePurpose
{
    public static async Task RunAsync(int processRunId)
    {
        var httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri(KeyConfig.ApiBaseUrl);

        var cacheService = new ApiAbstractionLicenceCacheService(httpClient);
        var outputService = new ApiOutputService(httpClient);
        var absOutputService = new ApiAbstractionLicenceOutputService(httpClient);
        
        var naldDataLookupService = new NaldDataLookupService(cacheService, absOutputService);
        var licenceList = await outputService.GetSimpleMatchResults(processRunId);
        var usedNaldPurposeIds = new List<string>();

        const string tKey = "Up to and Including ";
        
        Console.WriteLine($"{licenceList.Count} licences found to look at\n");
        var idx = 0;

        var onlyOneCount = 0;
        var explicitCount = 0;
        var notMatchedCount = 0;
        
        foreach (var licence in licenceList)
        {
            idx += 1;
            var matchesResult = await outputService.GetMatchesResultAsync(licence.FileId, processRunId);

            if (matchesResult == null)
            {
                Console.WriteLine($"{idx} - Skipping - Matches result is null");
                continue;
            }

            var licenceNumbers = new Dictionary<string, bool>();
            
            var scrapedLicenceNumber = matchesResult.Matches?
                .FirstOrDefault(m => m.LabelGroupName == "LicenceNumber")?
                .Text?
                .FirstOrDefault()?
                .Text;

            if (!string.IsNullOrWhiteSpace(scrapedLicenceNumber))
            {
                licenceNumbers.Add(scrapedLicenceNumber, false);
            }

            var filenameLicenceNumber = matchesResult.Filename?.Split("__")[0];
            
            if (!string.IsNullOrWhiteSpace(filenameLicenceNumber))
            {
                licenceNumbers.Add(filenameLicenceNumber, true);
            }
            
            if (licenceNumbers.Count == 0)
            {
                Console.WriteLine($"{idx} - Skipping - Licence number can't be found");
                continue;
            }
            
            NaldAbstractionData? naldDataLine = null;
            string? licenceNumberToUse = null;

            foreach (var licenceNumber in licenceNumbers)
            {
                licenceNumberToUse = licenceNumber.Key;
                
                naldDataLine = await naldDataLookupService.GetNaldAbstractionDataLineAsync(
                    licenceNumber.Key,
                    matchesResult.RegionCode,
                    licenceNumber.Value);

                if (naldDataLine != null)
                {
                    break;
                }
            }

            if (naldDataLine == null)
            {
                Console.WriteLine($"{idx} - Skipping - Cannot find in NALD for {licenceNumberToUse}");
                continue;
            }

            var purposesSection = matchesResult.Matches?
                .FirstOrDefault(result => result.LabelGroupName == "Purposes");

            if (purposesSection == null)
            {
                Console.WriteLine($"{idx} - Skipping - Purposes section is null");
                continue;
            }
            
            var naldPurposes = NaldDataLookupService.ToNaldPurposeData(naldDataLine?.Purposes);

            foreach (var purposePointGroup in purposesSection.SubResults)
            {
                var purposes = purposePointGroup.SubResults
                    .Where(x => x.MatchedLabelName == "Purposes")
                    .ToList();

                foreach (var purpose in purposes)
                {
                    var pointTextWithoutPurposeAndPoint = purpose.SubResults
                        .FirstOrDefault(x => x.MatchedLabelName == "TextWithoutPoints");
                
                    var tLines = pointTextWithoutPurposeAndPoint?
                        .Text?
                        .Select(t => t.Text)
                        .ToArray();
                    
                    var allTextWithoutNumber = tLines?
                        .Where(t => !t.StartsWith(tKey, StringComparison.OrdinalIgnoreCase))
                        .ToArray();
                    
                    var documentPurpose = allTextWithoutNumber != null
                        ? string.Join('\n', allTextWithoutNumber)
                        : null;

                    if (string.IsNullOrWhiteSpace(documentPurpose))
                    {
                        // TODO log
                        continue;
                    }
                    
                    var (naldPurposeData, matchType) =
                        await naldDataLookupService.GetRelevantNaldPurposesAsync(
                            naldPurposes,
                            documentPurpose,
                            usedNaldPurposeIds,
                            licenceNumberToUse!,
                            true);
                    
                    if (naldPurposeData.Length == 0)
                    {
                        Console.WriteLine($"{idx} - 0 nald purposes found for '{documentPurpose}'");
                        notMatchedCount++;
                        
                        continue;
                    }

                    Console.WriteLine($"{idx} - {naldPurposeData.Length} nald purposes found " +
                        $"for '{documentPurpose}' - match type '{matchType}'");                    
                    
                    switch (matchType)
                    {
                        case "OnlyOne":
                            onlyOneCount++;
                            break;
                        case "ExplicitMapping":
                            explicitCount++;
                            break;
                        default:
                            Console.WriteLine($"{idx} - ERROR {matchType} - Not supported");
                            break;
                    }
                }
            }
        }
        
        Console.WriteLine($"\n**Summary** Only one {onlyOneCount}, Explicit {explicitCount}");
    }
}