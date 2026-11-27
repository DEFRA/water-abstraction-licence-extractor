using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class AggregateHelper
{
    public static Aggregate[]? GetAggregates(
        IReadOnlyList<Licence> licences,
        bool excludeAnyLinksNotInSet)
    {
        var aggregates = new List<Aggregate>();

        foreach (var licence in licences)
        {
            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }

            var relevantAggregates = licence.AbstractionLimits.Aggregates;
            
            if (excludeAnyLinksNotInSet)
            {
                relevantAggregates = relevantAggregates
                    .Where(agg => agg.LinkedLicences == null
                        || agg.LinkedLicences.Length == 0
                        || agg.LinkedLicences.All(
                            lln => licences.Any(l => l.LicenceNumber?.Value == lln)))
                    .ToArray();
            }

            aggregates.AddRange(relevantAggregates);
        }

        aggregates = aggregates
            .Select(agg => agg.Clone())
            .ToList();
        
        foreach (var aggregate in aggregates)
        {
            aggregate.LicenceSetId = null;
        }

        return aggregates.Count == 0 ? null : aggregates.ToArray();
    }

    public static bool AreAggregatesFundamentallyEqual(
        Aggregate sourceAggregate,
        Aggregate otherAggregate)
    {
        var percentageLikeness = CalculateAggregatesSimilarity(sourceAggregate, otherAggregate);
        const int equalityCutoff = 100;
        
        return percentageLikeness == equalityCutoff;
    }
    
    public static bool AreAggregatesSimilarEnoughToGroup(
        Aggregate sourceAggregate,
        Aggregate otherAggregate)
    {
        var percentageLikeness = CalculateAggregatesSimilarity(sourceAggregate, otherAggregate);
        const int similarCutoff = 90;
        
        return percentageLikeness >= similarCutoff;
    }

    private static int CalculateAggregatesSimilarity(
        Aggregate sourceAggregate,
        Aggregate otherAggregate)
    {
        var percentageLikeness = 100;
        
        if (sourceAggregate.PrimaryType == PrimaryType.InLicence
            || otherAggregate.PrimaryType == PrimaryType.InLicence)
        {
            percentageLikeness -= 90;
        }
        
        if (sourceAggregate.LinkedLicences?.Length != otherAggregate.LinkedLicences?.Length)
        {
            percentageLikeness -= 5;
        }
        
        if (sourceAggregate.Limits.Count != otherAggregate.Limits.Count)
        {
            percentageLikeness -= 5;
        }

        if (sourceAggregate.LinkedLicences != null && otherAggregate.LinkedLicences != null)
        {
            const int onlyItemValue = 50;
            const int multipleItemsValue = 5;
            
            // Source contains a linked licence not in other
            foreach (var sourceLinkedLicence in sourceAggregate.LinkedLicences!)
            {
                if (sourceLinkedLicence != otherAggregate.SourceLicenceNumber
                    && !otherAggregate.LinkedLicences.Contains(sourceLinkedLicence))
                {
                    percentageLikeness -= sourceAggregate.LinkedLicences.Length == 1
                        ? onlyItemValue
                        : multipleItemsValue;
                }
            }

            // Other contains a linked licence not in source
            foreach (var otherLinkedLicence in otherAggregate.LinkedLicences!)
            {
                if (otherLinkedLicence != sourceAggregate.SourceLicenceNumber
                    && !sourceAggregate.LinkedLicences.Contains(otherLinkedLicence))
                {
                    percentageLikeness -= otherAggregate.LinkedLicences.Length == 1
                        ? onlyItemValue
                        : multipleItemsValue;
                }
            }
        }

        foreach (var sourceLimit in sourceAggregate.Limits)
        {
            if (otherAggregate.Limits.All(otherLimit => !otherLimit.Value.FloatingEqualTo(sourceLimit.Value)))
            {
                percentageLikeness -= 5;
            }
        }
        
        foreach (var otherLimit in otherAggregate.Limits)
        {
            if (sourceAggregate.Limits.All(sourceLimit => !sourceLimit.Value.FloatingEqualTo(otherLimit.Value)))
            {
                percentageLikeness -= 5;
            }
        }
        
        return percentageLikeness;
    }
    
    public static void SetDiscrepancyInfo(List<LicenceSet> licenceSets)
    {
        foreach (var licenceSet in licenceSets)
        {
            SetDiscrepancyInfo(licenceSet.Licences);
            
            if (licenceSet.AggregateSets == null)
            {
                continue;
            }

            foreach (var aggregate in licenceSet.AggregateSets!)
            {
                aggregate.SetHasDiscrepancy(null);
            }
        }
    }
    
    private static void SetDiscrepancyInfo(IEnumerable<Licence> licences)
    {
        var okayLicences = licences
            .Where(l => l.Status == ScrapeStatus.Ok)
            .ToList();

        foreach (var licence in okayLicences)
        {
            if (string.IsNullOrWhiteSpace(licence.LicenceNumber?.Value))
            {
                continue;
            }
            
            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }
            
            foreach (var aggregate in licence.AbstractionLimits.Aggregates)
            {
                aggregate.SetHasDiscrepancy(licence.LicenceNumber!.Value!);
            }
        }
    }

    public static void DeDuplicateAggregates(IEnumerable<Licence> licences)
    {
        var okayLicences = licences
            .Where(l => l.Status == ScrapeStatus.Ok)
            .ToList();

        foreach (var licence in okayLicences)
        {
            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }
            
            var groupedAggregates = licence.AbstractionLimits.Aggregates
                .GroupBy(a => a.Id)
                .ToArray();

            if (!groupedAggregates.Any(agg => agg.Count() > 1))
            {
                continue;
            }

            var newAggregates = new List<Aggregate>();

            foreach (var aggregateGroup in groupedAggregates)
            {
                if (aggregateGroup.Count() == 1)
                {
                    newAggregates.Add(aggregateGroup.First());
                    continue;
                }

                var containedInList = new List<ContainedInInformation>();

                foreach (var aggregate in aggregateGroup)
                {
                    if (aggregate.ContainedIn == null)
                    {
                        continue;
                    }
                    
                    containedInList.AddRange(aggregate.ContainedIn);
                }

                var returnAggregate = aggregateGroup.First();
                returnAggregate.ContainedIn = containedInList
                    .GroupBy(ci => new { ci.Source, ci.SectionName, ci.LineNumber, ci.DocumentIdentifier })
                    .Select(grp => grp.First())
                    .ToArray();
                
                newAggregates.Add(returnAggregate);
            }
            
            licence.AbstractionLimits.Aggregates = newAggregates.ToArray();
        }
    }

    public static List<Aggregate> GetSupersets(
        Aggregate aggregate,
        List<Aggregate> otherAggregates)
    {
        var returnList = new List<Aggregate>();
        
        var aggregateLinkedLicenceIds = (aggregate.LinkedLicences ?? []).ToList();
        aggregateLinkedLicenceIds.AddRange(aggregate.SourceLicenceNumber!);

        aggregateLinkedLicenceIds = aggregateLinkedLicenceIds
            .Distinct()
            .ToList();
        
        foreach (var otherAggregate in otherAggregates)
        {
            var otherAggregateSetLicenceIds = (otherAggregate.LinkedLicences ?? []).ToList();
            
            otherAggregateSetLicenceIds.AddRange(
                otherAggregates.Select(a => a.SourceLicenceNumber!));
            
            otherAggregateSetLicenceIds = otherAggregateSetLicenceIds
                .Distinct()
                .ToList();

            if (otherAggregateSetLicenceIds.Count == 0)
            {
                continue;
            }
            
            var notFound = false;

            foreach (var licenceId in aggregateLinkedLicenceIds)
            {
                if (otherAggregateSetLicenceIds.Contains(licenceId))
                {
                    continue;
                }
                
                notFound = true;
                break;
            }
            
            var hasMoreLicences = otherAggregateSetLicenceIds.Count > aggregateLinkedLicenceIds.Count;
            
            var sameCombinedValue = FloatingEqualTo(
                otherAggregates.Sum(a => a.GetCombinedLimitValue()),
                aggregate.GetCombinedLimitValue());
            
            if (!notFound
                && hasMoreLicences
                && sameCombinedValue)
            {
                returnList.Add(otherAggregate);
            }
        }

        return returnList;
    }

    public static List<LicenceSet> GetSupersets(LicenceSet licenceSet, List<LicenceSet> otherLicenceSets)
    {
        var returnList = new List<LicenceSet>();
        
        foreach (var otherLicenceSet in otherLicenceSets)
        {
            var otherLicenceSetLicenceIds = otherLicenceSet.Licences
                .Select(l => l.LicenceNumber?.Value)
                .ToList();
            
            var notFound = false;
            
            foreach (var licence in licenceSet.Licences)
            {
                if (otherLicenceSetLicenceIds.Contains(licence.LicenceNumber?.Value))
                {
                    continue;
                }
                
                notFound = true;
                break;
            }

            var hasMoreLicences = otherLicenceSet.Licences.Length > licenceSet.Licences.Length;
            var sameAggregateCount = otherLicenceSet.AggregateSets?.Length
                == licenceSet.AggregateSets?.Length;
            
            var otherLicenceSetCombinedValue =
                otherLicenceSet.AggregateSets?.Sum(agg => agg.GetCombinedLimitValue());
            
            var licenceSetCombinedValue =
                licenceSet.AggregateSets?.Sum(agg => agg.GetCombinedLimitValue());

            var sameCombinedValue = FloatingEqualTo(otherLicenceSetCombinedValue, licenceSetCombinedValue);
            var sameType = licenceSet.LicenceSetType == otherLicenceSet.LicenceSetType;
            
            if (!notFound
                && hasMoreLicences
                && sameAggregateCount
                && sameCombinedValue
                && sameType)
            {
                returnList.Add(otherLicenceSet);
            }
        }

        return returnList;
    }

    public static void UpdateLicenceAggregatesFromLicenceSets(List<LicenceSet> licenceSets)
    {
        foreach (var licenceSet in licenceSets)
        {
            if (licenceSet.AggregateSets == null)
            {
                continue;
            }

            foreach (var aggregate in licenceSet.AggregateSets!)
            {
                if (aggregate.LinkedLicences == null)
                {
                    continue;
                }

                var aggregateLicences = aggregate.LinkedLicences!.ToList();
                aggregateLicences.Add(aggregate.SourceLicenceNumber!);

                foreach (var linkedLicence in aggregateLicences)
                {
                    var licenceSetLicence = licenceSet.Licences
                        .FirstOrDefault(l => l.LicenceNumber?.Value == linkedLicence);

                    if (licenceSetLicence?.AbstractionLimits.Aggregates == null)
                    {
                        continue;
                    }

                    var newAggregates = new List<Aggregate>();

                    var licenceAlreadyHasAggregate = licenceSetLicence.AbstractionLimits.Aggregates!
                        .Any(agg => AreAggregatesSimilarEnoughToGroup(agg, aggregate));

                    foreach (var licenceAggregate in licenceSetLicence.AbstractionLimits.Aggregates!)
                    {
                        var matchingVariations = aggregate.Variations?
                            .Where(v => v.Id == licenceAggregate.Id)
                            .ToList() ?? [];

                        if (matchingVariations.Count == 0)
                        {
                            newAggregates.Add(licenceAggregate);
                        }

                        var missingLinkedLicences = aggregate.LinkedLicences
                            .Where(ll => licenceSetLicence.LinkedLicences.All(ll2 => ll2.LicenceNumber != ll)
                                         && ll != licenceSetLicence.LicenceNumber?.Value)
                            .ToList();

                        if (missingLinkedLicences.Count == 0)
                        {
                            if (!newAggregates.Contains(licenceAggregate))
                            {
                                newAggregates.Add(licenceAggregate);
                            }

                            if (!licenceAlreadyHasAggregate)
                            {
                                newAggregates.Add(aggregate);
                            }

                            continue;
                        }

                        newAggregates.Add(aggregate);

                        var newLinkedLicences = new List<LinkedLicence>();
                        newLinkedLicences.AddRange(licenceSetLicence.LinkedLicences);
                        newLinkedLicences.AddRange(missingLinkedLicences
                            .Select(missingLinkedLicenceNumber =>
                            {
                                var missingLinkedLicenceStep1 = licenceSet.Licences
                                    .FirstOrDefault(l => l.LinkedLicences.Any(ll =>
                                        ll.LicenceNumber == missingLinkedLicenceNumber));

                                var missingLinkedLicence = missingLinkedLicenceStep1?
                                    .LinkedLicences
                                    .FirstOrDefault(ll => ll.LicenceNumber == missingLinkedLicenceNumber);

                                if (missingLinkedLicence == null)
                                {
                                    // TODO look in to how this can happen / log
                                    return null;
                                }

                                // 1/25/03/031/R02 is a rare (only?) case of this

                                var containedIn = missingLinkedLicence.ContainedIn!
                                    .Select(ci =>
                                    {
                                        var cloned = ci.Clone();
                                        cloned.Direction = InformationDirection.ReferencedAlongside;
                                        cloned.SourceLicence = missingLinkedLicenceStep1!.LicenceNumber?.Value;

                                        return cloned;
                                    })
                                    .ToArray();

                                return new LinkedLicence
                                {
                                    LicenceNumber = missingLinkedLicenceNumber,
                                    RegionId = licenceSetLicence.RegionId,
                                    IsBecauseOfAggregate = true,
                                    ContainedIn = containedIn
                                };
                            })
                            .Where(x => x != null)
                            .Select(x => x!));

                        licenceSetLicence.LinkedLicences = newLinkedLicences.ToArray();
                    }

                    var newNewAggregates = new List<Aggregate>();

                    foreach (var newAggregate in newAggregates)
                    {
                        var otherAggregates = newAggregates
                            .Where(agg => agg != newAggregate)
                            .ToList();

                        var supersets = GetSupersets(newAggregate, otherAggregates);

                        if (supersets.Count == 0)
                        {
                            newNewAggregates.Add(newAggregate);
                        }
                        else
                        {
                            foreach (var superset in supersets)
                            {
                                var contained = superset.Variations?.Any(v => v.Id == newAggregate.Id) == true;

                                if (contained)
                                {
                                    continue;
                                }

                                var variations = new List<AggregateVariation>();

                                if (superset.Variations != null)
                                {
                                    variations.AddRange(superset.Variations);
                                }

                                variations.Add(AggregateVariation.FromAggregate(
                                    newAggregate,
                                    AggregateVariation.DifferentOrder));

                                superset.Variations = variations.ToArray();
                            }
                        }
                    }

                    newAggregates = newNewAggregates;

                    licenceSetLicence.AbstractionLimits.Aggregates = newAggregates.Count > 0
                        ? newAggregates.ToArray()
                        : null;
                }
            }
        }
    }

    private static bool FloatingEqualTo(this double? value1, double? value2)
    {
        return Math.Abs((value1 ?? 0) - (value2 ?? 0)) < 0.0000001; 
    }
}
