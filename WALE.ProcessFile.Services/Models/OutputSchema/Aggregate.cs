using System.Text;
using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Aggregate
{
    public string Id
    {
        get
        {
            var type = PrimaryType switch
            {
                Enums.OutputSchema.PrimaryType.LicenceToLicence => "L2L",
                Enums.OutputSchema.PrimaryType.InLicence => "IL",
                _ => throw new ArgumentOutOfRangeException()
            };

            var licenceNumber = LicenceNumber?
                .Replace("/", string.Empty)
                .Replace(" ", string.Empty);

            var outputSb = new StringBuilder();
            
            foreach (var linkedLicence in LinkedLicences)
            {
                var linkedLicenceNumber = linkedLicence.LicenceNumber?
                    .Replace("/", string.Empty)
                    .Replace(" ", string.Empty);
                
                outputSb.Append($"-{linkedLicenceNumber}");
            }

            return $"{licenceNumber}{LicenceVersionId}-{type}{outputSb}";
        }
    }
    
    public string? GroupId { get; set; }
    
    public string? LicenceNumber { get; set; }
    
    public string? LicenceVersionId { get; set; }
    
    public PrimaryType? PrimaryType { get; set; }
    
    public SubType? SubType { get; set; }
    
    public string? NaldType { get; set; }
    
    public TimePeriod? TimePeriod { get; set; }

    public TimeCutoff? TimeCutoff { get; set; }
    
    public Purpose[]? Purposes { get; set; }
    
    public Point[]? Points { get; set; }
    
    public LinkedLicence[] LinkedLicences { get; set; } = [];
    
    public AbstractionLimit[] Limits { get; set; } = [];
}