using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Helpers;

public class NaldLinkedLicenceHelper
{
    private readonly Dictionary<string, HashSet<NaldLicence>> _linkedLicenceMap;
    private readonly string _processingRegionCode;

    private NaldLinkedLicenceHelper(Dictionary<string, HashSet<NaldLicence>> linkedLicenceMap, string processingRegionCode)
    {
        _linkedLicenceMap = linkedLicenceMap;
        _processingRegionCode = processingRegionCode;
    }

    public static async Task<NaldLinkedLicenceHelper> CreateAsync(List<NaldLinkedLicenceRawData> rawData,
        string processingRegionCode)
    {
        var map = await BuildLinkedLicenceMapAsync(rawData, processingRegionCode);
        return new NaldLinkedLicenceHelper(map, processingRegionCode);
    }

    public async Task<List<NaldLicence>> GetLinkedLicencesAsync(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber))
        {
            return [];
        }

        var naldLicences = await LicenceNumber.GetNaldLicencesAsync(licenceNumber, _processingRegionCode);
        var candidateLicenceNumbers = naldLicences
            .Select(l => l.LicenceNumber)
            .ToList();

        if (candidateLicenceNumbers.Count != 1)
        {
            return [];
        }

        return _linkedLicenceMap.TryGetValue(candidateLicenceNumbers[0], out var linked)
            ? linked.ToList()
            : [];
    }

    private static async Task<Dictionary<string, HashSet<NaldLicence>>> BuildLinkedLicenceMapAsync(
        List<NaldLinkedLicenceRawData> rawData, string processingRegionCode)
    {
        var map = new Dictionary<string, HashSet<NaldLicence>>();

        foreach (var item in rawData)
        {
            if (item.RegionCode != processingRegionCode)
            {
                continue;
            }
            
            var licNo = item.LicenceNumber;
            
            if (string.IsNullOrEmpty(licNo))
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
                var linkCandidates = await LicenceNumber.ExtractNaldLicencesAsync(text);
                
                foreach (var linkCandidate in linkCandidates)
                {
                    if (licNo != linkCandidate.LicenceNumber || linkCandidate.RegionCode != processingRegionCode)
                    {
                        if (!map.ContainsKey(licNo))
                        {
                            map[licNo] = [];
                        }
                        
                        map[licNo].Add(linkCandidate);
                    }
                }
            }
        }

        return map;
    }
}