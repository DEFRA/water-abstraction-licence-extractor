using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Enums;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class AbstractionLimitHelper
{
    public static AbstractionLimit? GetAbstractionLimit(
        double? value,
        LimitPeriodType periodType,
        Purpose[] purposes,
        Point[] points,
        ContainedInInformation containedIn)
    {
        if (value == null)
        {
            return null;
        }

        const string litres = "litres";
        const string cubicMeters = "cubic metres";

        return new AbstractionLimit
        {
            Units = periodType == LimitPeriodType.PerSecond ? litres : cubicMeters,
            Value = value,
            PeriodType = periodType,
            Purposes = purposes,
            Points = points,
            ContainedIn = [containedIn]
        };
    }
    public static bool AreLimitsEqual(AbstractionLimit limitLeft, AbstractionLimit limitRight)
    {
        return limitLeft.PeriodType == limitRight.PeriodType
               && AreValuesEqual(
                   ValueInBaseUnits(limitLeft.Value, limitLeft.Units),
                   ValueInBaseUnits(limitRight.Value, limitRight.Units));
    }
    public static bool GroupContainsLimit(AbstractionLimitGroup group, AbstractionLimit limitToFind)
    {
        return group.Limits.Any(groupLimit => AreLimitsEqual(groupLimit, limitToFind));
    }
    public static void RemoveDuplicateLimitsPerGroup(AbstractionLimitGroup[] individual)
    {
        foreach (var grp in individual)
        {
            grp.Limits = grp.Limits
                .GroupBy(l => new
                {
                    l.Value,
                    l.Units,
                    l.PeriodType,
                    Points = l.Points?
                        .Select(p => $"{p.Id}_{p.NaldId}")
                        .ToArray(),
                    Purposes = l.Purposes?
                        .Select(p => $"{p.Id}_{string.Join('_', p.NaldIds ?? [])}")
                        .ToArray()
                })
                .Select(g => g.First())
                .ToList();
        }
    }
    public static void NullOutLimitLevelPointsAndPurposesIfRelevant(
        AbstractionLimitGroup limitGroup,
        List<AbstractionLimit> abstractionLimits)
    {
        if (limitGroup.Purposes?.Length > 0)
        {
            foreach (var aggregateLimit in abstractionLimits)
            {
                aggregateLimit.Purposes = null;
            }
        }
        else if (limitGroup.Purposes?.Length == 0)
        {
            limitGroup.Purposes = null;
        }

        if (limitGroup.Points?.Length > 0)
        {
            foreach (var aggregateLimit in abstractionLimits)
            {
                aggregateLimit.Points = null;
            }
        }
        else if (limitGroup.Points?.Length == 0)
        {
            limitGroup.Points = null;
        }
    }
    public static AbstractionLimitGroup[] AddNaldLimits(
        NaldAbstractionData? naldData,
        AbstractionLimitGroup[] individuals,
        Aggregate[] aggregates)
    {
        if (naldData == null)
        {
            return individuals;
        }

        var individualsList = individuals.ToList();

        var existingLimitGroups = individuals.ToList();
        existingLimitGroups.AddRange(aggregates);

        var periodTypes = new List<LimitPeriodType>
        {
            LimitPeriodType.PerYear,
            LimitPeriodType.PerDay,
            LimitPeriodType.PerHour,
            LimitPeriodType.PerSecond
        };

        var containedIn = new ContainedInInformation
        {
            Source = InformationSource.Nald
        };

        var limits = new List<AbstractionLimit>();

        foreach (var periodType in periodTypes)
        {
            var groupedNaldLimits = naldData.Purposes
                .GroupBy(l =>
                {
                    return periodType switch
                    {
                        LimitPeriodType.PerYear => l.Quantity.AnnualQty,
                        LimitPeriodType.PerDay => l.Quantity.DailyQty,
                        LimitPeriodType.PerHour => l.Quantity.HourlyQty,
                        LimitPeriodType.PerSecond => l.Quantity.InstQty,
                        _ => throw new Exception("Period type not known")
                    };
                })
                .Where(g => g.Key != null)
                .ToList();

            // Get Nald limits
            foreach (var groupedLimit in groupedNaldLimits)
            {
                var purposes = groupedLimit
                    .Select(x => new Purpose
                    {
                        NaldIds = [x.Id.ToString()] // TOOD check the grouping here
                    })
                    .ToArray();

                var points = groupedLimit
                    .SelectMany(nl => nl.PointIds)
                    .Distinct()
                    .Select(p => new Point { NaldId = p.ToString() })
                    .ToArray();

                var limit = GetAbstractionLimit(
                    groupedLimit.Key,
                    periodType,
                    purposes,
                    points,
                    containedIn);

                if (limit != null)
                {
                    limits.Add(limit);
                }
            }
        }

        AddNaldLimits(
            limits,
            existingLimitGroups,
            ref individualsList);

        return individualsList.ToArray();
    }
    public static void AddNaldLimits(
        List<AbstractionLimit> naldLimits,
        List<AbstractionLimitGroup> existingLimitGroups,
        ref List<AbstractionLimitGroup> individuals)
    {
        var unmatchedNaldLimits = new List<AbstractionLimit>();

        foreach (var naldLimit in naldLimits)
        {
            var matchingIndividualGroups = existingLimitGroups
                .Where(individualGroup => GroupContainsLimit(individualGroup, naldLimit))
                .ToList();

            if (matchingIndividualGroups.Count >= 1)
            {
                foreach (var matchingIndividualGroup in matchingIndividualGroups)
                {
                    // At the group level
                    if (matchingIndividualGroup.ContainedIn?.Length >= 1)
                    {
                        var containedInList = matchingIndividualGroup.ContainedIn!.ToList();

                        if (containedInList.Any(ci => ci.Source == InformationSource.Nald))
                        {
                            // Already have it in contained in
                            continue;
                        }

                        containedInList.Add(new ContainedInInformation
                        {
                            Source = InformationSource.Nald
                        });

                        matchingIndividualGroup.ContainedIn = containedInList.ToArray();
                        continue;
                    }

                    // At a limit level
                    foreach (var matchingIndividualGroupLimit in matchingIndividualGroup.Limits)
                    {
                        var containedInList = matchingIndividualGroupLimit.ContainedIn!.ToList();

                        if (containedInList.Any(ci => ci.Source == InformationSource.Nald))
                        {
                            // Already have it in contained in
                            continue;
                        }

                        containedInList.Add(new ContainedInInformation
                        {
                            Source = InformationSource.Nald
                        });

                        matchingIndividualGroupLimit.ContainedIn = containedInList.ToArray();
                    }
                }

                continue;
            }

            unmatchedNaldLimits.Add(naldLimit);
        }

        if (unmatchedNaldLimits.Count == 0)
        {
            return;
        }

        var newGroup = new AbstractionLimitGroup
        {
            Limits = unmatchedNaldLimits
        };

        individuals.Add(newGroup);
    }
    public static bool AreValuesEqual(double? value1, double? value2)
    {
        if (value1 == null || value2 == null)
        {
            return false;
        }

        // Tolerances work for comparing 0.42 (document) and 0.417 (NALD) in one of the tests + 41369.419 and 41360
        const double maxTolerance = 0.75;

        var percentageDiff = Math.Abs(100.0 - (100.0 / value1.Value) * value2.Value);
        return percentageDiff is >= 0 and <= maxTolerance;
    }
    public static double? ValueInBaseUnits(double? value, string? units)
    {
        const string baseUnit = "litres";

        if (string.IsNullOrEmpty(units))
        {
            return value;
        }

        try
        {
            return UnitHelper.Convert(value!.Value, units, baseUnit);
        }
        catch (Exception ex)
        {
            // AreValuesEqual treats null as "not equal", so an unconvertible limit stops matching
            // rather than matching wrongly.
            ConsoleHelper.WriteLine(
                $"WARNING - {nameof(AbstractionLimitHelper)} - cannot convert "
                + $"'{units}' to {baseUnit}, limit left unconverted - {ex.Message}");

            return null;
        }
    }

    public static void AddLicencesReferencedInLimitsToLicenceSets(
        List<LicenceSet> licenceSets,
        Licence primaryLicence,
        List<Licence> allLicences,
        List<LicenceSet> explicitlyReferencedLimitsLicenceSets)
    {
        var licenceSetsInLimits = licenceSets
            .Where(ls => ls.LicenceSetType == LicenceSetType.LicencesGroupedByAbstractionLimits)
            .ToList();

        if (explicitlyReferencedLimitsLicenceSets.Count == 0 || licenceSetsInLimits.Count == 0)
        {
            return;
        }

        var updatedLicencesReferencedInLimits = primaryLicence.LinkedLicences
            .Where(linkedLicence =>
                linkedLicence.ContainedIn?.Any(ci =>
                    linkedLicence.IsBecauseOfAggregate == true
                    || ci.SectionName == DocumentSectionNames.AbstractionLimits) == true)
            .Select(ll => ll.LicenceNumber)
            .Select(ln => allLicences.FirstOrDefault(l => l.LicenceNumber?.Value == ln))
            .Where(ln => ln != null)
            .Select(ln => ln!)
            .ToList();

        var allLicenceSetInLimitsLicences = licenceSetsInLimits
            .SelectMany(ls => ls.Licences)
            .ToList();

        var missingLicences = updatedLicencesReferencedInLimits
            .Where(l => allLicenceSetInLimitsLicences.All(ll => l.LicenceNumber?.Value != ll.LicenceNumber?.Value))
            .ToList();

        if (missingLicences.Count < 1)
        {
            return;
        }

        // Just add them to the first group, as we don't know which to add them to - this logic might need
        // revisting/expanding in future
        var firstLicenceSetInLimits = licenceSetsInLimits[0];

        var newLicences = new List<Licence>();
        newLicences.AddRange(firstLicenceSetInLimits.Licences);
        newLicences.AddRange(missingLicences);

        firstLicenceSetInLimits.Licences = newLicences.ToArray();
        LinkedLicenceAssertions.AssertNoDuplicateLicences(firstLicenceSetInLimits.Licences);
    }

    public static void HoistContainedInSections(
        AbstractionLimitGroup[] individuals,
        Aggregate[] aggregates)
    {
        var allLimitGroups = individuals.ToList();
        allLimitGroups.AddRange(aggregates);

        foreach (var limitGroup in allLimitGroups)
        {
            var allSame = true;

            var firstLimit = limitGroup.Limits.First();
            var firstLimitProviders = string.Join('_',
                firstLimit.ContainedIn!.Select(ci => $"{ci.Source}-{ci.SectionName}-{ci.LinkReason}"));

            foreach (var limit in limitGroup.Limits.Skip(1))
            {
                var limitProviders = string.Join('_',
                    limit.ContainedIn!.Select(ci => $"{ci.Source}-{ci.SectionName}-{ci.LinkReason}"));

                if (limitProviders != firstLimitProviders)
                {
                    allSame = false;
                    break;
                }
            }

            if (!allSame)
            {
                limitGroup.ContainedIn =
                [
                    new()
                    {
                        Source = InformationSource.MixedSourcesOrMixedReasons
                    }
                ];

                continue;
            }

            limitGroup.ContainedIn = firstLimit.ContainedIn;

            foreach (var limit in limitGroup.Limits)
            {
                limit.ContainedIn = null;
            }
        }
    }
}
