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
                var allLicenceNumbers = new List<string> { aggregate.SourceLicenceNumber! };
                allLicenceNumbers.AddRange(aggregate.LinkedLicences.Select(x => x.LicenceNumber) ?? []);
                
                return string.Join(',', allLicenceNumbers.OrderBy(lln => lln));
            })
            .Select(group => group.First());

        var licencesDict = new Dictionary<string, string>();
        
        foreach (var licence in groupedAggregates)
        {
            if (licence.SourceLicenceNumber == null)
            {
                // Shouldn't get here ideally
                Console.WriteLine("WARNING - AggregateSet - LicenceNumber is null");
                continue;
            }
            
            var sourceLicenceNumber = FormattingHelper.RemoveSeperators(licence.SourceLicenceNumber)!;
            
            if (licencesDict.ContainsKey(sourceLicenceNumber))
            {
                continue;
            }
            
            licencesDict.Add(sourceLicenceNumber, licence.SourceLicenceVersionId!);

            if (licence.LinkedLicences != null)
            {
                foreach (var linkedLicence in licence.LinkedLicences
                    .Select(ll => FormattingHelper.RemoveSeperators(ll.LicenceNumber)!))
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

        AggregateSetId = outputSb.ToString();
        return AggregateSetId;
    }

    private string? _aggregateSetId;
    
    public string? AggregateSetId
    {
        get => _aggregateSetId;
        // ReSharper disable once MemberCanBePrivate.Global - can't make private as used in serialisation
        set
        {
            _aggregateSetId = value;
            
            foreach (var aggregate in Aggregates)
            {
                aggregate.AggregateSetId = value;
            }
        }
    }
    
    public AggregateWithContext[] Aggregates { get; init; } = [];
}