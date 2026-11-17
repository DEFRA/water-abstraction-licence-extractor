using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

/// <summary>
/// Guards that throw rather than return, so a violation fails the file.
/// </summary>
public static class AggregateAssertions
{
    public static void AssertNoDuplicateAggregates(IEnumerable<Licence> licences)
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
                .GroupBy(v => v.Id)
                .Where(v => v.Count() > 1)
                .ToList();
            
            if (groupedAggregates.Any())
            {
                throw new Exception($"Grouped aggregates found for {licence.LicenceNumber?.Value}");
            }
        }
    }

    public static void AssertNoDuplicateVariations(IEnumerable<Licence> licences)
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
                
                var groupedVariations = aggregate.Variations
                    .GroupBy(v => v.Id)
                    .Where(v => v.Count() > 1)
                    .ToList();

                if (groupedVariations.Any())
                {
                    throw new Exception($"Grouped variations found in {aggregate.Id}");
                }
            }
        }
    }

    public static void AssertNoMultipleLevelVariations(IEnumerable<Licence> licences)
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

                foreach (var variation in aggregate.Variations)
                {
                    if (variation.Variations == null)
                    {
                        continue;
                    }

                    if (variation.Variations.Length >= 1)
                    {
                        throw new Exception("Multi level variation detected");
                    }
                }
            }
        }
    }

    public static void AssertNoMultipleLevelVariations(IEnumerable<LicenceSet> licenceSets)
    {
        foreach (var licenceSet in licenceSets)
        {
            AssertNoMultipleLevelVariations(licenceSet.Licences);
            
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

                foreach (var variation in aggregate.Variations)
                {
                    if (variation.Variations == null)
                    {
                        continue;
                    }

                    if (variation.Variations.Length >= 1)
                    {
                        throw new Exception("Multi level variation detected");
                    }
                }
            }
        }
    }
}
