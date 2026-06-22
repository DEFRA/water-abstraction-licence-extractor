using System.Text;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class AggregateSet
{
    public string SetAggregateSetId(IReadOnlyList<Licence> allLicences)
    {
        var groupedAggregates = Aggregates
            .GroupBy(aggregate =>
            {
                var allLicenceNumbers = new List<string> { aggregate.LicenceNumber! };
                allLicenceNumbers.AddRange(aggregate.LinkedLicences ?? []);
                
                return string.Join(',', allLicenceNumbers.OrderBy(lln => lln));
            })
            .Select(group => group.First());

        var licencesDict = new Dictionary<string, string>();
        
        foreach (var licence in groupedAggregates)
        {
            if (licence.LicenceNumber == null)
            {
                // Shouldn't get here ideally
                Console.WriteLine("WARNING - AggregateSet - LicenceNumber is null");
                continue;
            }
            
            if (licencesDict.ContainsKey(licence.LicenceNumber))
            {
                continue;
            }
            
            licencesDict.Add(licence.LicenceNumber!, licence.LicenceVersionId!);

            if (licence.LinkedLicences != null)
            {
                foreach (var linkedLicence in licence.LinkedLicences)
                {
                    if (licencesDict.ContainsKey(linkedLicence))
                    {
                        continue;
                    }

                    var lookedUpLicence = allLicences.FirstOrDefault(
                        al => al.LicenceNumber?.Value == linkedLicence);

                    licencesDict.Add(linkedLicence,
                        lookedUpLicence?.LicenceVersion.LicenceVersionId ?? LicenceVersion.UnknownVersion);
                }
            }
        }
            
        var licencesAlphabetical = licencesDict
            .OrderBy(licence => $"{licence.Key}-{licence.Value}");

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

        AggregateSetId = outputSb.ToString();
        return AggregateSetId;
    }

    private string? _aggregateSetId;
    
    public string? AggregateSetId
    {
        get => _aggregateSetId;
        private set
        {
            _aggregateSetId = value;
            
            foreach (var aggregate in Aggregates)
            {
                aggregate.AggregateSetId = value;
            }
        }
    }
    
    /*public string? VersionNumber { get; set; }*/

    public AggregateWithContext[] Aggregates { get; init; } = [];
}