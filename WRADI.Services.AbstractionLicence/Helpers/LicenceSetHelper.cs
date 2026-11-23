using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.DocumentType.AbstractionLicence.Helpers;

public static class LicenceSetHelper
{
    public static List<LicenceSet> WithRedundantSingleSetsRemoved(
        List<LicenceSet> licenceSets,
        string? keepSingleSetForLicenceNumber)
    {
        var allNoneSingleSets = licenceSets
            .Where(licenceSet => licenceSet.LicenceSetType != LicenceSetType.SingleLicenceOnly)
            .ToList();

        if (allNoneSingleSets.Count == 0)
        {
            return licenceSets;
        }

        if (keepSingleSetForLicenceNumber == null)
        {
            return allNoneSingleSets;
        }

        var licenceNumbersInGroupedSets = allNoneSingleSets
            .SelectMany(licenceSet => licenceSet.Licences)
            .Select(licence => licence.LicenceNumber?.Value)
            .ToHashSet();

        // A document describing other licences' shared limits produces a grouped set it is not
        // itself a member of; without this its own licence ends up in no set and is never saved.
        return licenceSets
            .Where(licenceSet => licenceSet.LicenceSetType != LicenceSetType.SingleLicenceOnly
                                 || (licenceSet.Licences.Any(licence =>
                                         licence.LicenceNumber?.Value == keepSingleSetForLicenceNumber)
                                     && !licenceNumbersInGroupedSets.Contains(keepSingleSetForLicenceNumber)))
            .ToList();
    }

    public static List<LicenceSet> AsDistinctLicenceSets(List<IReadOnlyList<LicenceSet>> licenceSetGroups)
    {
        var returnList = new List<LicenceSet>();

        foreach (var licenceSetGroup in licenceSetGroups)
        {
            foreach (var licenceSet in licenceSetGroup)
            {
                if (returnList.Any(dls => dls.LicenceSetId == licenceSet.LicenceSetId))
                {
                    continue;
                }

                returnList.Add(licenceSet);
            }
        }

        return returnList;
    }

    public static List<LicenceSet> PreferLargerLicenceSets(
        List<LicenceSet> licenceSets,
        LicenceSetType licenceSetType)
    {
        var returnLicenceSets = new List<LicenceSet>();
        returnLicenceSets.AddRange(licenceSets
            .Where(ls => ls.LicenceSetType != licenceSetType));
        
        var lineageLicenceSets = licenceSets
            .Where(ls => ls.LicenceSetType == licenceSetType)
            .ToList();
        
        foreach (var licenceSet in lineageLicenceSets)
        {
            var (hasLargerEncompassingSet, _) = EncompassingLicenceSetExists(
                lineageLicenceSets,
                licenceSet.Licences.Select(l => l.LicenceNumber?.Value).ToArray()!);

            var encompassesOtherSet = EncompassesAnotherLicenceSet(
                lineageLicenceSets,
                licenceSet);

            if (encompassesOtherSet)
            {
                foreach (var licence in licenceSet.Licences)
                {
                    licence.LicenceSets = licence.LicenceSets
                        .Where(ls => ls.LicenceSetId == licenceSet.LicenceSetId
                            || ls.LicenceSetType != LicenceSetType.LicencesGroupedByLineage)
                        .ToArray();
                }
            }
            
            if (hasLargerEncompassingSet)
            {
                foreach (var licence in licenceSet.Licences)
                {
                    licence.LicenceSets = licence.LicenceSets
                        .Where(ls => ls.LicenceSetId != licenceSet.LicenceSetId)
                        .ToArray();
                }
            }
            
            if (hasLargerEncompassingSet)
            {
                continue;
            }
            
            returnLicenceSets.Add(licenceSet);
        }
        
        return returnLicenceSets;
    }

    private static (bool Exists, string? SetId) EncompassingLicenceSetExists(
        List<LicenceSet> lineageLicenceSets,
        string[] mustContainLicenceNumbers)
    {
        string? matchId = null;
        
        var exists = lineageLicenceSets.Any(ls =>
        {
            var biggerGroup = ls.Licences.Length > mustContainLicenceNumbers.Length;

            if (!biggerGroup)
            {
                return false;
            }

            foreach (var mustContainLicenceNumber in mustContainLicenceNumbers)
            {
                if (ls.Licences.All(l => l.LicenceNumber?.Value != mustContainLicenceNumber))
                {
                    return false;
                }
            }

            matchId = ls.LicenceSetId;
            return true;
        });

        return (exists, matchId);
    }

    private static bool EncompassesAnotherLicenceSet(
        List<LicenceSet> lineageLicenceSets,
        LicenceSet licenceSet)
    {
        var encompassesAny = lineageLicenceSets.Any(ls =>
        {
            var (exists, setId) = EncompassingLicenceSetExists(
                lineageLicenceSets,
                ls.Licences.Select(l => l.LicenceNumber?.Value).ToArray()!);

            return exists && setId == licenceSet.LicenceSetId;
        });

        return encompassesAny;
    }
}
