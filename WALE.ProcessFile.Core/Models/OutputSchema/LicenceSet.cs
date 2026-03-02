using System.Text;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LicenceSet
{
    public string LicenceSetId
    {
        get
        {
            var licencesAlphabetical = Licences
                .OrderBy(licence =>
                {
                    var licenceNumber = FormattingHelper.RemoveSeperators(licence.LicenceNumber?.Value);
                    return licenceNumber + licence.LicenceVersion.LicenceVersionId;
                });

            var outputSb = new StringBuilder();
            
            foreach (var licence in licencesAlphabetical)
            {
                if (outputSb.Length > 0)
                {
                    outputSb.Append('-');
                }

                var licenceNumber = FormattingHelper.RemoveSeperators(licence.LicenceNumber?.Value);
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
                .OrderBy(licence => licence.LicenceNumber?.Value + licence.LicenceVersion.LicenceVersionId);

            var outputSb = new StringBuilder();
            
            foreach (var licence in licencesAlphabetical)
            {
                if (outputSb.Length > 0)
                {
                    outputSb.Append('-');
                }

                var licenceNumberParts = licence.LicenceNumber?.Value?
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