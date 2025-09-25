using System.Text;
using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class Aggregate : AbstractionLimitGroup
{
    public string Id
    {
        get
        {
            var primaryType = PrimaryType switch
            {
                PrimaryType.LicenceToLicence => "LL",
                PrimaryType.InLicence => "IL",
                PrimaryType.NotSet => "NS",
                _ => throw new ArgumentOutOfRangeException()
            };
            
            var subType = SubType switch
            {
                Enums.OutputSchema.SubType.PointToPoint => "PO",
                Enums.OutputSchema.SubType.PurposeToPurpose => "PU",
                Enums.OutputSchema.SubType.NotSet => "NS",
                _ => string.Empty
            };

            var licenceNumber = LicenceNumber?
                .Replace("/", string.Empty)
                .Replace(" ", string.Empty);

            var outputSb = new StringBuilder();

            if (LinkedLicences != null)
            {
                foreach (var linkedLicence in LinkedLicences)
                {
                    var linkedLicenceNumber = linkedLicence.LicenceNumber?
                        .Replace("/", string.Empty)
                        .Replace(" ", string.Empty);

                    outputSb.Append($"-{linkedLicenceNumber}");
                }
            }

            return $"{licenceNumber}-{LicenceVersionId}-{primaryType}{subType}{outputSb}";
        }
    }
    
    public string? AggregateSetId { get; set; }
    
    public string? LicenceNumber { get; init; }
    
    public string? LicenceVersionId { get; init; }
    
    public PrimaryType PrimaryType { get; init; }
    
    public SubType? SubType { get; set; }
    
    public string? NaldType { get; set; }

    public TimeCutoff? TimeCutoff { get; set; }
    
    public Purpose[]? Purposes { get; set; } = [];

    public Point[]? Points { get; set; } = [];
    
    public LinkedLicence[]? LinkedLicences { get; init; } = [];

    public new static Aggregate Template => new()
    {
        AggregateSetId = string.Empty,
        NaldType = null,
        PrimaryType = PrimaryType.NotSet,
        SubType = Enums.OutputSchema.SubType.NotSet,
        TimeCutoff = new TimeCutoff
        {
            Date = null,
            CutoffType = CutoffType.Unknown
        },
        TimePeriod = new TimePeriod
        {
            StartDate = null,
            EndDate = null
        },
        LicenceNumber = null,
        LicenceVersionId = null,
        Limits = [AggregateAbstractionLimit.Template],
        LinkedLicences = []
    };
}