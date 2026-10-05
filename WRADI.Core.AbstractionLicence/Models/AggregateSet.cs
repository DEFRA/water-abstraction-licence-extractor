using System.Text;
using WALE.ProcessFile.Core.Helpers;

namespace WRADI.Core.AbstractionLicence.Models;

public class AggregateSet
{
    public string SetAggregateSetId(IReadOnlyList<Licence> allLicences)
    {
        var groupedAggregates = Aggregates
            .GroupBy(aggregate =>
            {
                var allLicenceNumbers = new List<string> { aggregate.SourceLicenceNumber! };
                allLicenceNumbers.AddRange(aggregate.LinkedLicences ?? []);
                
                var licenceNumbersStr = string.Join(',', allLicenceNumbers.OrderBy(lln => lln));
                return $"{licenceNumbersStr}-{aggregate.GetCombinedLimitValue()}";
            })
            .Select(group => group.First());

        var combinedValue = 0.0;
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
        AggregateSetId = outputSb.ToString();

        return AggregateSetId;
    }

    public string? AggregateSetId
    {
        get;
        // ReSharper disable once MemberCanBePrivate.Global - can't make private as used in serialisation
        set
        {
            field = value;

            foreach (var aggregate in Aggregates)
            {
                aggregate.AggregateSetId = value;
            }
        }
    }

    public AggregateWithContext[] Aggregates { get; set; } = [];
}