using System.Text;
using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models.OutputSchema;

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
    
    public string ShortLicenceSetId
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

                var licenceNumberParts = licence.LicenceNumber?
                    .Replace(" ", "/")
                    .Replace(".", "/")
                    .Split('/');

                var part = licenceNumberParts?.Last();

                if (part?.StartsWith("R0") == true)
                {
                    part = licenceNumberParts![licenceNumberParts.Length - 2];
                }
                
                outputSb.Append(part);
            }

            return outputSb.ToString();
        }
    }

    public LicenceSetType[] LicenceSetTypes { get; set; } = [];
    
    public AggregateSet[]? AggregateSets { get; set; } = [];
    
    public Licence[] Licences { get; set; } = [];
}