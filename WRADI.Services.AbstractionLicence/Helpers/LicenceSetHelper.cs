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
}
