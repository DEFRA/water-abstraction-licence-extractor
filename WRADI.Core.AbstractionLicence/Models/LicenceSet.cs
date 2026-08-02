using System.Text;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class LicenceSet
{
    public string LicenceSetId
    {
        get
        {
            var licencesAlphabetical = Licences.OrderBy(licence => licence.Id);
            var outputSb = new StringBuilder();
            
            foreach (var licence in licencesAlphabetical)
            {
                if (outputSb.Length > 0)
                {
                    outputSb.Append('-');
                }

                outputSb.Append(licence.Id);
            }

            return outputSb.ToString();
        }
    }
    
    public string ShortLicenceSetId
    {
        get
        {
            var licencesAlphabetical = Licences.OrderBy(licence => licence.Id);
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