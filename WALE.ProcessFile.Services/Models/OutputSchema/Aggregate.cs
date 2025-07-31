using System.Text;
using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Aggregate
{
    public string Id
    {
        get
        {
            var primaryType = PrimaryType switch
            {
                Enums.OutputSchema.PrimaryType.LicenceToLicence => "LL",
                Enums.OutputSchema.PrimaryType.InLicence => "IL",
                _ => throw new ArgumentOutOfRangeException()
            };
            
            var subType = SubType switch
            {
                Enums.OutputSchema.SubType.PointToPoint => "PO",
                Enums.OutputSchema.SubType.PurposeToPurpose => "PU",
                _ => string.Empty
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

            return $"{licenceNumber}{LicenceVersionId}-{primaryType}{subType}{outputSb}";
        }
    }
    
    public string? AggregateSetId { get; set; }
    
    public string? LicenceNumber { get; init; }
    
    public string? LicenceVersionId { get; init; }
    
    public PrimaryType? PrimaryType { get; init; }
    
    public SubType? SubType { get; init; }
    
    public string? NaldType { get; set; }

    public TimeLimited? TimeCutoff { get; set; }
    
    public Purpose[] Purposes { get; set; } = [];

    public Point[] Points { get; set; } = [];
    
    public TimePeriod? TimePeriod { get; set; }
    
    public LinkedLicence[] LinkedLicences { get; init; } = [];
    
    public AggregateAbstractionLimit[] Limits { get; init; } = [];
}