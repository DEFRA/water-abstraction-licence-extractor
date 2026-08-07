using System.Text;
using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

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
                Enums.SubType.PointToPoint => "PO",
                Enums.SubType.PurposeToPurpose => "PU",
                Enums.SubType.NotSet => "NS",
                _ => string.Empty
            };

            var licenceNumber = SourceLicenceNumber?
                .Replace("/", string.Empty)
                .Replace(" ", string.Empty);

            var outputSb = new StringBuilder();

            if (LinkedLicences != null)
            {
                foreach (var linkedLicence in LinkedLicences)
                {
                    var linkedLicenceNumber = linkedLicence
                        .Replace("/", string.Empty)
                        .Replace(" ", string.Empty);

                    outputSb.Append($"-{linkedLicenceNumber}");
                }
            }

            return $"{licenceNumber}-{SourceLicenceVersionId}-{primaryType}{subType}{outputSb}";
        }
    }
    
    public string? AggregateSetId { get; set; }
    
    public string? SourceLicenceNumber { get; set; }
    
    public string? SourceLicenceVersionId { get; set; }
    
    public bool? IsExplicitlyAggregate { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PrimaryType PrimaryType { get; init; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubType? SubType { get; set; }
    
    public string? NaldType { get; set; }
    
    public string[]? LinkedLicences { get; init; } = [];

    public new static Aggregate Template => new()
    {
        AggregateSetId = string.Empty,
        NaldType = null,
        PrimaryType = PrimaryType.NotSet,
        SubType = Enums.SubType.NotSet,
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
        SourceLicenceNumber = null,
        SourceLicenceVersionId = null,
        Limits = [AbstractionLimit.Template],
        DocumentIdentifier = null,
        LinkedLicences = []
    };
}