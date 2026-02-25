using System.Text;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class AggregateSet
{
    public void SetAggregateSetId(IReadOnlyList<Licence> allLicences)
    {
        var groupedAggregates = Aggregates
            .GroupBy(aggregate => aggregate.AggregateSetId)
            .Select(group => group.First());

        var licencesDict = new Dictionary<string, string>();
        
        foreach (var licence in groupedAggregates)
        {
            if (licence.LicenceNumber == null)
            {
                // TODO log, shouldn't get here ideally
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
                    if (licencesDict.ContainsKey(linkedLicence.LicenceNumber!))
                    {
                        continue;
                    }

                    var lookedUpLicence =
                        allLicences.FirstOrDefault(l => l.LicenceNumber == linkedLicence.LicenceNumber);
                    licencesDict.Add(linkedLicence.LicenceNumber!,
                        lookedUpLicence?.LicenceVersion.LicenceVersionId ?? LicenceVersion.UnknownVersion);
                }
            }
        }
            
        var licencesAlphabetical = licencesDict
            .OrderBy(licence => licence.Key + licence.Value);

        var outputSb = new StringBuilder();
        
        foreach (var (key, licenceVersionId) in licencesAlphabetical)
        {
            if (outputSb.Length > 0)
            {
                outputSb.Append('-');
            }

            var licenceNumber = key
                .Replace(" ", string.Empty)
                .Replace("/", string.Empty);

            outputSb.Append($"{licenceNumber}-{licenceVersionId}");
        }

        AggregateSetId = outputSb.ToString();
    }

    public string? AggregateSetId { get; set; }
    
    /*public string? VersionNumber { get; set; }*/

    public Aggregate[] Aggregates { get; init; } = [];
}