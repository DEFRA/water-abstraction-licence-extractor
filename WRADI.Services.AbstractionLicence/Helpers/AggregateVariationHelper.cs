using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

/// <summary>
/// An aggregate's variations: the same limit reached by a different route.
/// </summary>
public static class AggregateVariationHelper
{
    public static AggregateVariation[]? DeDupeVariations(AggregateVariation[]? variations)
    {
        return variations?
            .GroupBy(v => v.Id)
            .Select(v => v.First())
            .ToArray();
    }

    public static void RemoveDuplicateVariations(IEnumerable<Licence> licences)
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
            
            foreach (var aggregate in licence.AbstractionLimits.Aggregates)
            {
                if (aggregate.Variations == null)
                {
                    continue;
                }

                aggregate.Variations = aggregate.Variations
                    .GroupBy(v => v.Id)
                    .Select(g => g.First())
                    .ToArray();;
            }
        }
    }

    public static void SquashTwoLevelVariations(List<LicenceSet> licenceSets)
    {
        foreach (var licenceSet in licenceSets)
        {
            SquashTwoLevelVariations(licenceSet.Licences);
                
            if (licenceSet.AggregateSets == null)
            {
                continue;
            }
            
            foreach (var aggregate in licenceSet.AggregateSets)
            {
                if (aggregate.Variations == null)
                {
                    continue;
                }
                
                aggregate.Variations = SquashTwoLevelVariations(aggregate.Variations).ToArray();
            }
        }
    }

    public static void SquashTwoLevelVariations(IEnumerable<Licence> licences)
    {
        foreach (var licence in licences)
        {
            if (licence.AbstractionLimits.Aggregates == null)
            {
                continue;
            }
            
            foreach (var aggregate in licence.AbstractionLimits.Aggregates)
            {
                if (aggregate.Variations == null)
                {
                    continue;
                }
                
                aggregate.Variations = SquashTwoLevelVariations(aggregate.Variations).ToArray();
            }
        }
    }

    public static List<AggregateVariation> SquashTwoLevelVariations(IEnumerable<AggregateVariation> variations)
    {
        var newVariations = new List<AggregateVariation>();
        var variationsList = variations.ToList();
                    
        foreach (var variation in variationsList)
        {
            newVariations.Add(variation);

            if (!(variation.Variations?.Length > 0))
            {
                continue;
            }

            var uniqueVariations = variation.Variations
                .Where(vv =>
                {
                    var contained = variationsList.Any(v => v.Id == vv.Id);
                    return !contained;
                })
                .ToList();
                        
            newVariations.AddRange(uniqueVariations);
            variation.Variations = null;
        }

        return newVariations;
    }

    public static void AddVariationsToLicenceAggregates(List<Licence> allLicences)
    {
        foreach (var sourceLicence in allLicences)
        {
            var sourceAggregates = sourceLicence.AbstractionLimits.Aggregates;
            if (sourceAggregates == null || sourceAggregates.Length == 0)
            {
                continue;
            }

            var otherLicences = allLicences.Except([sourceLicence]).ToList();

            foreach (var otherLicence in otherLicences)
            {
                var otherAggregates = otherLicence.AbstractionLimits.Aggregates;
                if (otherAggregates == null || otherAggregates.Length == 0)
                {
                    continue;
                }

                foreach (var otherAggregate in otherAggregates)
                {
                    var matchingSourceAggregate = sourceAggregates
                        .FirstOrDefault(sourceAggregate => AggregateHelper.AreAggregatesFundamentallyEqual(
                            sourceAggregate,
                            otherAggregate));

                    if (matchingSourceAggregate == null)
                    {
                        continue;
                    }
                    
                    if (matchingSourceAggregate.Variations?.Any(
                        v => v.Id == otherAggregate.Id) == true)
                    {
                        continue;
                    }

                    var variations = new List<AggregateVariation>();

                    if (matchingSourceAggregate.Variations != null)
                    {
                        variations.AddRange(matchingSourceAggregate.Variations);
                    }
                    
                    variations.Add(AggregateVariation.FromAggregate(
                        otherAggregate,
                        AggregateVariation.DifferentOrder));

                    variations = SquashTwoLevelVariations(variations)
                        .Where(v => v.Id != matchingSourceAggregate.Id)
                        .ToList();
                        
                    matchingSourceAggregate.Variations = variations.Count >= 1 ? variations.ToArray() : null;
                }
            }
        }
    }
}
