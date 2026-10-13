using System.Text;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Core.AbstractionLicence.Helpers;

public static class AggregateSetHelper
{
    public static string GetAggregateSetId(IReadOnlyList<Licence> allLicences, List<Aggregate> aggregates)
    {
        var groupedAggregates = aggregates
            .GroupBy(aggregate =>
            {
                var allLicenceNumbers = new List<string> { aggregate.SourceLicenceNumber! };
                allLicenceNumbers.AddRange(aggregate.LinkedLicences ?? []);
                
                var licenceNumbersStr = string.Join(',', allLicenceNumbers.OrderBy(lln => lln));
                return $"{licenceNumbersStr}-{aggregate.GetCombinedLimitValue()}";
            })
            .Select(group => group.First());

        var combinedValue = 0;
        var licencesDict = new Dictionary<string, string>();
        
        foreach (var aggregate in groupedAggregates)
        {
            combinedValue += aggregate.GetCombinedLimitValue();
            
            if (aggregate.SourceLicenceNumber == null)
            {
                // Shouldn't get here ideally
                Console.WriteLine("WARNING - AggregateSet - LicenceNumber is null");
                continue;
            }
            
            var sourceLicenceNumber = FormattingHelper.RemoveSeperators(aggregate.SourceLicenceNumber)!;
            
            if (licencesDict.ContainsKey(sourceLicenceNumber))
            {
                continue;
            }
            
            licencesDict.Add(sourceLicenceNumber, aggregate.SourceLicenceVersionId!);

            if (aggregate.LinkedLicences != null)
            {
                foreach (var linkedLicence in aggregate.LinkedLicences
                    .Select(ll => FormattingHelper.RemoveSeperators(ll)!))
                {
                    if (licencesDict.ContainsKey(linkedLicence))
                    {
                        continue;
                    }

                    var lookedUpLicence = allLicences.FirstOrDefault(
                        al => FormattingHelper.RemoveSeperators(al.LicenceNumber?.Value) == linkedLicence);

                    licencesDict.Add(linkedLicence,
                        lookedUpLicence?.LicenceVersion.LicenceVersionId ?? LicenceVersion.UnknownVersion);
                }
            }
        }
            
        var licencesAlphabetical = licencesDict
            .OrderBy(licence => $"{FormattingHelper.RemoveSeperators(licence.Key)}-{licence.Value}");

        var outputSb = new StringBuilder();
        
        foreach (var (licenceNumber, licenceVersionId) in licencesAlphabetical)
        {
            if (outputSb.Length > 0)
            {
                outputSb.Append('-');
            }

            var licenceNumberOutput = FormattingHelper.RemoveSeperators(licenceNumber);
            outputSb.Append($"{licenceNumberOutput}-{licenceVersionId}");
        }

        outputSb.Append($"-C{combinedValue}");
        return outputSb.ToString();
    }
}