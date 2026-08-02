using WALE.ProcessFile.Services.Formats;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Formats;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public class NaldLinkedLicenceHelper
{
    private readonly Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>> _linkedLicenceMap;

    private NaldLinkedLicenceHelper(Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>> linkedLicenceMap)
    {
        _linkedLicenceMap = linkedLicenceMap;
    }

    public static async Task<NaldLinkedLicenceHelper> CreateAsync(
        IAbstractionLicenceCacheService cacheService)
    {
        var rawData =
            await cacheService.GetNaldLinkedLicenceRawDataAsync();
        
        var map = BuildLinkedLicenceMap(rawData);
        return new NaldLinkedLicenceHelper(map);
    }

    public List<NaldLinkedLicence> GetLinkedLicences(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return [];
        }

        var naldLicences = AbstractionLicenceNumber.GetNaldLicences(licenceNumber);
        var candidateLicenceNumbers = naldLicences
            .Select(l => l.LicenceNumber)
            .ToList();

        var returnList = new List<NaldLinkedLicence>();

        foreach (var candidateLicenceNumber in candidateLicenceNumbers)
        {
            var values = _linkedLicenceMap.TryGetValue(candidateLicenceNumber, out var linkedDict)
                ? linkedDict.Values.SelectMany(v => v).ToList()
                : [];
            
            returnList.AddRange(values);
        }

        return returnList;
    }

    private static Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>> BuildLinkedLicenceMap(
        List<NaldLinkedLicenceRawData> naldRawData)
    {
        var map = new Dictionary<string, Dictionary<string, List<NaldLinkedLicence>>>();

        foreach (var naldRawDataItem in naldRawData)
        {
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
                var linkCandidates = AbstractionLicenceNumber.ExtractNaldLicences(text);

                foreach (var linkCandidate in linkCandidates)
                {
                    if (forwardLinkKey == linkCandidate.LicenceNumber)
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
                        FromField = potentialNumberSource.Key,
                        FromFieldText = potentialNumberSource.Value
                    });

                    backMap.TryAdd(forwardLinkKey, []);
                    
                    // Add back link
                    backMap[forwardLinkKey].Add(new NaldLinkedLicence
                    {
                        NaldLicence = naldRawDataItem.ToNaldLicence(),
                        LinkType = NaldLinkedLicenceType.Incoming,
                        FromField = potentialNumberSource.Key,
                        FromFieldText = potentialNumberSource.Value,
                        IncomingLicenceNumber = forwardLinkKey
                    });
                }
            }
        }

        return map;
    }
}