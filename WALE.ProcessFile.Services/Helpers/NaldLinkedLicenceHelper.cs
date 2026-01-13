using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;

namespace WALE.ProcessFile.Services.Helpers;

public class NaldLinkedLicenceHelper
{
    private readonly Dictionary<string, HashSet<string>> _linkedLicenceMap;

    public NaldLinkedLicenceHelper(List<NaldLinkedLicenceRawData> rawData)
    {
        _linkedLicenceMap = BuildLinkedLicenceMap(rawData);
    }

    public List<string> GetLinkedLicences(string? licenceNumber)
    {
        if (string.IsNullOrEmpty(licenceNumber)) return [];

        var stripped = FormattingHelper.StripForComparison(licenceNumber);
        if (stripped != null && _linkedLicenceMap.TryGetValue(stripped, out var linked))
        {
            return linked.ToList();
        }

        return [];
    }

    private Dictionary<string, HashSet<string>> BuildLinkedLicenceMap(List<NaldLinkedLicenceRawData> rawData)
    {
        var map = new Dictionary<string, HashSet<string>>();

        foreach (var item in rawData)
        {
            if (string.IsNullOrEmpty(item.LicenceNumber)) continue;

            var strippedLicNo = FormattingHelper.StripForComparison(item.LicenceNumber);
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
                var licenceNumbers = LicenceNumber.FindLicenceNumbers(text);
                foreach (var licenceNumber in licenceNumbers)
                {
                    var strippedAggLicNo = FormattingHelper.StripForComparison(licenceNumber);

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