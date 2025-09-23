using System.Text;
using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class LicenceSet
{
    public string LicenceSetId
    {
        get
        {
            var licencesAlphabetical = Licences
                .OrderBy(licence => licence.LicenceNumber + licence.LicenceVersion.LicenceVersionId);

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

                var licenceVersionId = licence.LicenceVersion.LicenceVersionId;
                outputSb.Append($"{licenceNumber}-{licenceVersionId}");
            }

            return outputSb.ToString();
        }
    }
    
    public LicenceSetType LicenceSetType { get; init; }
    
    public AggregateSet[] AggregateSets { get; init; } = [];
    
    public Licence[] Licences { get; init; } = [];
}