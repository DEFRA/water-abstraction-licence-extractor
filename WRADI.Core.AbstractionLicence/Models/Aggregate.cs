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

            var linkedLicencesSb = new StringBuilder();

            if (LinkedLicences != null)
            {
                foreach (var linkedLicence in LinkedLicences)
                {
                    var linkedLicenceNumber = linkedLicence
                        .Replace("/", string.Empty)
                        .Replace(" ", string.Empty);

                    linkedLicencesSb.Append($"-{linkedLicenceNumber}");
                }
            }

            var outputSb = new StringBuilder();
            outputSb.Append($"{licenceNumber}-{SourceLicenceVersionId}-{primaryType}{subType}");
            outputSb.Append(linkedLicencesSb);
            
            if (!string.IsNullOrWhiteSpace(DocumentIdentifier))
            {
                outputSb.Append($"-{DocumentIdentifier.Replace(".", "_")}");
            }
            
            outputSb.Append($"-C{GetCombinedLimitValue()}");
            return outputSb.ToString();
        }
    }

    public double GetCombinedLimitValue()
    {
        var combinedValue = 0.0;
        
        foreach (var limit in Limits)
        {
            if (limit.Value == null)
            {
                continue;
            }

            combinedValue += limit.Value!.Value;
        }

        return combinedValue;
    }
    
    public string? AggregateSetId { get; set; }
    
    public string? SourceLicenceNumber { get; set; }
    
    public string? SourceLicenceVersionId { get; set; }
    
    public bool? IsExplicitlyAggregate { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PrimaryType PrimaryType { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubType? SubType { get; set; }
    
    public string? NaldType { get; set; }
    
    public string[]? LinkedLicences { get; set; } = [];
    
    public AggregateVersion[]? OtherVersions { get; set; }

    public Aggregate Clone()
    {
        var returnItem = new Aggregate();
        CloneProperties(this, returnItem);
        
        return returnItem;
    }
    
    public static void CloneProperties(
        Aggregate sourceAggregate,
        Aggregate destinationAggregate)
    {
        destinationAggregate.AggregateSetId = sourceAggregate.AggregateSetId;
        destinationAggregate.SourceLicenceNumber = sourceAggregate.SourceLicenceNumber;
        destinationAggregate.SourceLicenceVersionId = sourceAggregate.SourceLicenceVersionId;
        destinationAggregate.IsExplicitlyAggregate = sourceAggregate.IsExplicitlyAggregate;
        destinationAggregate.PrimaryType = sourceAggregate.PrimaryType;
        destinationAggregate.SubType = sourceAggregate.SubType;
        destinationAggregate.NaldType = sourceAggregate.NaldType;
        destinationAggregate.LinkedLicences = sourceAggregate.LinkedLicences;
        destinationAggregate.OtherVersions = sourceAggregate.OtherVersions;        
        
        AbstractionLimitGroup.CloneProperties(
            sourceAggregate,
            destinationAggregate);
    }
    
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