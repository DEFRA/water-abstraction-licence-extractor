using System.Text;
using System.Text.Json.Serialization;
using WRADI.Core.AbstractionLicence.Constants;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Helpers;

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
                foreach (var linkedLicence in LinkedLicences.OrderBy(ll => ll))
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
            
            outputSb.Append($"-C{(long)GetCombinedLimitValue()}");
            return outputSb.ToString();
        }
    }

    // Only is set when inside a Licence rather then inside a LicenceSet
    public string? LicenceSetId { get; set; }

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
    
    public string? SourceLicenceNumber { get; set; }
    
    public string? SourceLicenceVersionId { get; set; }
    
    public bool? IsExplicitlyAggregate { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public PrimaryType PrimaryType { get; set; }
    
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public SubType? SubType { get; set; }
    
    public string? NaldType { get; set; }
    
    public string[]? LinkedLicences { get; set; } = [];

    public AggregateVariation[]? Variations { get; set; }

    public bool? HasDiscrepancy { get; private set; }
    
    public string? DiscrepancyReason { get; private set; }

    public void SetHasDiscrepancy(string? sourceLicenceNumber)
    {
        var noOutgoing = ContainedIn?
            .All(ci => ci.Direction != InformationDirection.Outgoing) == true;

        if (noOutgoing)
        {
            HasDiscrepancy = true;
            DiscrepancyReason = DiscrepancyReasons.NoLinkBack;
            
            return;
        }

        if (Variations == null || Variations.Length == 0)
        {
            HasDiscrepancy = true;
            return;
        }

        var linkedLicencesStr = GetOrderedLicenceNumberString(this);
        var combinedTotal = GetCombinedLimitValue();
        
        foreach (var variation in Variations)
        {
            var variationLinkedLicencesStr = GetOrderedLicenceNumberString(variation);
            
            var diffLinkedLicences = linkedLicencesStr != variationLinkedLicencesStr;
            
            if (diffLinkedLicences)
            {
                HasDiscrepancy = true;
                
                if (linkedLicencesStr.Length > variationLinkedLicencesStr.Length)
                {
                    DiscrepancyReason = DiscrepancyReasons.IsSuperset; // TODO more checks to confirm this
                }
                else
                {
                    DiscrepancyReason = DiscrepancyReasons.DiffLinkedLicences;
                }

                return;
            }
            
            var variationCombinedTotal = variation.GetCombinedLimitValue();
            var diffCombinedTotal = !ComparisonHelper.AreValuesEqual(combinedTotal, variationCombinedTotal);
            
            if (diffCombinedTotal)
            {
                HasDiscrepancy = true;
                DiscrepancyReason = DiscrepancyReasons.CombinedTotalDifferent;
                
                return;
            }
            
            // TODO check on other properties too
        }
        
        HasDiscrepancy = false;
        DiscrepancyReason = null;
    }

    private static string GetOrderedLicenceNumberString(Aggregate aggregate)
    {
        var allLicences = (aggregate.LinkedLicences ?? []).ToList();
        
        if (!string.IsNullOrWhiteSpace(aggregate.SourceLicenceNumber))
        {
            allLicences.Add(aggregate.SourceLicenceNumber);
        }

        return string.Join('_', allLicences.OrderBy(ll => ll));
    }

    // Placeholder for user-entered aggregate description - not populated in scrape output.
    public string? Description { get; set; }

    public Aggregate Clone()
    {
        var returnItem = new Aggregate();
        CloneProperties(this, returnItem);
        
        return returnItem;
    }

    protected static void CloneProperties(
        Aggregate sourceAggregate,
        Aggregate destinationAggregate)
    {
        destinationAggregate.LicenceSetId = sourceAggregate.LicenceSetId;
        destinationAggregate.SourceLicenceNumber = sourceAggregate.SourceLicenceNumber;
        destinationAggregate.SourceLicenceVersionId = sourceAggregate.SourceLicenceVersionId;
        destinationAggregate.IsExplicitlyAggregate = sourceAggregate.IsExplicitlyAggregate;
        destinationAggregate.PrimaryType = sourceAggregate.PrimaryType;
        destinationAggregate.SubType = sourceAggregate.SubType;
        destinationAggregate.NaldType = sourceAggregate.NaldType;
        destinationAggregate.LinkedLicences = sourceAggregate.LinkedLicences?.ToArray();
        destinationAggregate.Variations = sourceAggregate.Variations?.ToArray();
        destinationAggregate.Description = sourceAggregate.Description;
        
        AbstractionLimitGroup.CloneProperties(
            sourceAggregate,
            destinationAggregate);
    }
    
    public new static Aggregate Template => new()
    {
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