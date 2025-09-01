using System.Text;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class AggregateSet
{
    public string AggregateSetId
    {
        get
        {
            var licencesAlphabetical = Aggregates
                .GroupBy(aggregate => aggregate.AggregateSetId)
                .Select(group => group.First())
                .OrderBy(licence => licence.LicenceNumber + licence.LicenceVersionId);

            var outputSb = new StringBuilder();
            
            foreach (var licence in licencesAlphabetical)
            {
                if (outputSb.Length > 0)
                {
                    outputSb.Append('-');
                }

                var licenceNumber = licence.LicenceNumber?
                    .Replace(" ", string.Empty)
                    .Replace("/", string.Empty);

                var licenceVersionId = licence.LicenceVersionId;
                
                outputSb.Append($"{licenceNumber}-{licenceVersionId}");
            }

            return outputSb.ToString();
        }
    }
    
    /*public string? VersionNumber { get; set; }*/

    public Aggregate[] Aggregates { get; init; } = [];
}