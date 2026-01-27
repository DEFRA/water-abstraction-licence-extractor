using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Helpers;

public class NaldLinkedLicenceHelper
{
    private readonly Dictionary<string, HashSet<string>> _linkedLicenceMap;

    private NaldLinkedLicenceHelper(Dictionary<string, HashSet<string>> linkedLicenceMap)
    {
        _linkedLicenceMap = linkedLicenceMap;
    }

    public static async Task<NaldLinkedLicenceHelper> CreateAsync(List<NaldLinkedLicenceRawData> rawData)
    {
        var map = await BuildLinkedLicenceMapAsync(rawData);
        return new NaldLinkedLicenceHelper(map);
    }

    public async Task<List<string>> GetLinkedLicencesAsync(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber)) return [];

        var stripped = FormattingHelper.StripForComparison(licenceNumber, regionCode);
        if (stripped != null && _linkedLicenceMap.TryGetValue(stripped, out var linked))
        {
            return linked.ToList();
        }

        return [];
    }

    private static async Task<Dictionary<string, HashSet<string>>> BuildLinkedLicenceMapAsync(List<NaldLinkedLicenceRawData> rawData)
    {
        var map = new Dictionary<string, HashSet<string>>();

        foreach (var item in rawData)
        {
            if (string.IsNullOrEmpty(item.LicenceNumber)) continue;

            var strippedLicNo = FormattingHelper.StripForComparison(item.LicenceNumber,regionCode);
            if (strippedLicNo == null) continue;

            if (!map.ContainsKey(strippedLicNo))
            {
                map[strippedLicNo] = [];
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
                var licenceNumbers = await LicenceNumber.FindLicenceNumbersAsync(text);
                foreach (var licenceNumber in licenceNumbers)
                {
                    var strippedAggLicNo = FormattingHelper.StripForComparison(licenceNumber, regionCode);

                    if (strippedAggLicNo != null && strippedAggLicNo != strippedLicNo)
                    {
                        map[strippedLicNo].Add(strippedAggLicNo);
                    }
                }
            }
        }
        
        return map;
    }
}