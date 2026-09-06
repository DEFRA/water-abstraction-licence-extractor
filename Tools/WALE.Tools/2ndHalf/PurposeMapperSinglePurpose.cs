using WALE.ProcessFile.Services.Output;
using WALE.Tools.Config;
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
        
        foreach (var licence in licenceList)
        {
            var matchesResult = await outputService.GetMatchesResultAsync(licence.FileId, processRunId);

            if (matchesResult == null)
            {
                // TODO log
                continue;
            }
            
            var licenceNumber = matchesResult.Matches?
                .FirstOrDefault(m => m.LabelGroupName == "LicenceNumber")?
                .Text?
                .FirstOrDefault()?
                .Text;

            if (string.IsNullOrWhiteSpace(licenceNumber))
            {
                // TODO log
                continue;
            }
            
            var naldDataLine = await naldDataLookupService.GetNaldAbstractionDataLineAsync(
                licenceNumber,
                matchesResult.RegionCode);
            
            var purposesSection = matchesResult.Matches?.FirstOrDefault(result => result.LabelGroupName == "Purposes");

            if (purposesSection == null)
            {
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
                    
                    var (naldPurposeData, _) = await naldDataLookupService.GetRelevantNaldPurposesAsync(
                        naldPurposes,
                        documentPurpose,
                        usedNaldPurposeIds,
                        licenceNumber,
                        true);
                    //...
                }
            }
        }
    }
}