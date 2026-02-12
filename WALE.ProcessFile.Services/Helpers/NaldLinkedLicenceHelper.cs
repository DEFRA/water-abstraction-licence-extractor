using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Helpers;

public class NaldLinkedLicenceHelper
{
    private readonly Dictionary<string, Dictionary<string, NaldLinkedLicence>> _linkedLicenceMap;
    private readonly short _processingRegionCode;

    private NaldLinkedLicenceHelper(Dictionary<string, Dictionary<string, NaldLinkedLicence>> linkedLicenceMap,
        short processingRegionCode)
    {
        _linkedLicenceMap = linkedLicenceMap;
        _processingRegionCode = processingRegionCode;
    }

    public static async Task<NaldLinkedLicenceHelper> CreateAsync(
        ICacheService cacheService,
        short processingRegionCode)
    {
        var rawData =
            await cacheService.GetNaldLinkedLicenceRawDataAsync(processingRegionCode);
        
        var map = BuildLinkedLicenceMap(rawData, processingRegionCode);
        return new NaldLinkedLicenceHelper(map, processingRegionCode);
    }

    public List<NaldLinkedLicence> GetLinkedLicences(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return [];
        }

        var naldLicences = LicenceNumber.GetNaldLicences(licenceNumber, _processingRegionCode);
        var candidateLicenceNumbers = naldLicences
            .Select(l => l.LicenceNumber)
            .ToList();

        if (candidateLicenceNumbers.Count != 1)
        {
            return [];
        }

        return _linkedLicenceMap.TryGetValue(candidateLicenceNumbers[0], out var linked)
            ? linked.Values.ToList()
            : [];
    }

    private static Dictionary<string, Dictionary<string, NaldLinkedLicence>> BuildLinkedLicenceMap(
        List<NaldLinkedLicenceRawData> rawData,
        short processingRegionCode)
    {
        var map = new Dictionary<string, Dictionary<string, NaldLinkedLicence>>();

        foreach (var item in rawData)
        {
            if (item.RegionCode != processingRegionCode)
            {
                continue;
            }

            var forwardLinkKey = item.LicenceNumber;

            if (string.IsNullOrEmpty(forwardLinkKey))
            {
                continue;
            }

            var potentialNumbers = new List<string?>
            {
                item.Param1,
                item.Param2,
                item.Text,
                item.Notes
            };

            foreach (var text in potentialNumbers)
            {
                var linkCandidates = LicenceNumber.ExtractNaldLicences(text);

                foreach (var linkCandidate in linkCandidates)
                {
                    if (forwardLinkKey != linkCandidate.LicenceNumber ||
                        linkCandidate.RegionCode != processingRegionCode)
                    {
                        var backLinkKey = linkCandidate.LicenceNumber;

                        // Ensure map keys are initialized in both directions
                        map.TryAdd(forwardLinkKey, []);
                        map.TryAdd(backLinkKey, []);

                        // Add forward link (or update if it exists already - a previous iteration may have added it as a back link)
                        map[forwardLinkKey][backLinkKey] = new NaldLinkedLicence
                        {
                            NaldLicence = linkCandidate,
                            LinkType = NaldLinkedLicenceType.Explicit
                        };

                        // Add back link, but only if no forward link already exists (achieved by using TryAdd, which does nothing if the key already exists)
                        map[backLinkKey].TryAdd(forwardLinkKey, new NaldLinkedLicence
                        {
                            NaldLicence = item.ToNaldLicence(),
                            LinkType = NaldLinkedLicenceType.BackLink
                        });
                    }
                }
            }
        }

        return map;
    }
}