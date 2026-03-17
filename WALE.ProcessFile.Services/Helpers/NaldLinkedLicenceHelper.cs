using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Helpers;

public class NaldLinkedLicenceHelper
{
    private readonly Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>> _linkedLicenceMap;
    private readonly short _processingRegionCode;

    private NaldLinkedLicenceHelper(Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>> linkedLicenceMap,
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
            ? linked.Values.SelectMany(v => v).ToList()
            : [];
    }

    private static Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>> BuildLinkedLicenceMap(
        List<NaldLinkedLicenceRawData> naldRawData,
        short processingRegionCode)
    {
        var map = new Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>>();

        foreach (var naldRawDataItem in naldRawData)
        {
            if (naldRawDataItem.RegionCode != processingRegionCode)
            {
                continue;
            }

            var forwardLinkKey = naldRawDataItem.LicenceNumber;
            
            if (string.IsNullOrEmpty(forwardLinkKey))
            {
                continue;
            }

            var potentialNumberSources = new Dictionary<string, string?>
            {
                { nameof(naldRawDataItem.Param1), naldRawDataItem.Param1 },
                { nameof(naldRawDataItem.Param2), naldRawDataItem.Param2 },
                { nameof(naldRawDataItem.Text), naldRawDataItem.Text },
                { nameof(naldRawDataItem.Notes), naldRawDataItem.Notes }
            };

            foreach (var potentialNumberSource in potentialNumberSources)
            {
                var text = potentialNumberSource.Value;
                var linkCandidates = LicenceNumber.ExtractNaldLicences(text);

                foreach (var linkCandidate in linkCandidates)
                {
                    if (forwardLinkKey == linkCandidate.LicenceNumber
                        && linkCandidate.RegionCode == processingRegionCode)
                    {
                        continue;
                    }
                    
                    var backLinkKey = linkCandidate.LicenceNumber;
                    
                    // Ensure map keys are initialized in both directions
                    map.TryAdd(forwardLinkKey, []);
                    map.TryAdd(backLinkKey, []);

                    var forwardMap = map[forwardLinkKey];
                    var backMap = map[backLinkKey];

                    forwardMap.TryAdd(backLinkKey, []);
                    
                    // Add forward link
                    forwardMap[backLinkKey].Add(new NaldLinkedLicence
                    {
                        NaldLicence = linkCandidate,
                        LinkType = NaldLinkedLicenceType.Outgoing,
                        FromField = potentialNumberSource.Key
                    });

                    backMap.TryAdd(forwardLinkKey, []);
                    
                    // Add back link
                    backMap[forwardLinkKey].Add(new NaldLinkedLicence
                    {
                        NaldLicence = naldRawDataItem.ToNaldLicence(),
                        LinkType = NaldLinkedLicenceType.Incoming,
                        FromField = potentialNumberSource.Key,
                        IncomingLicenceNumber = forwardLinkKey
                    });
                }
            }
        }

        return map;
    }
}