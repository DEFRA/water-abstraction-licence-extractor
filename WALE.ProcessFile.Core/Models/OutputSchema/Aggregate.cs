using System.Text;
using System.Text.Json.Serialization;
using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

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
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PrimaryType PrimaryType { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubType? SubType { get; set; }
    
    public string? NaldType { get; set; }

    public TimeCutoff? TimeCutoff { get; set; }
    
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
        Limits = [AbstractionLimit.Template],
        DocumentIdentifier = null,
        LinkedLicences = []
    };
}