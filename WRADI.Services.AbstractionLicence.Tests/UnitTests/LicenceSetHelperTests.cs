using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class LicenceSetHelperTests
{
    [Fact]
    public void WhenNoGroupedSetContainsIt_ThenTheNamedLicenceKeepsItsSingleSet()
    {
        var licenceSets = new List<LicenceSet>
        {
            SingleSet("1/01/01/001"),
            GroupedSet("1/01/01/002", "1/01/01/003")
        };

        var result = LicenceSetHelper.WithRedundantSingleSetsRemoved(licenceSets, "1/01/01/001");

        Assert.Equal(2, result.Count);
        Assert.Contains(result, set => set.LicenceSetType == LicenceSetType.SingleLicenceOnly
                                       && LicenceNumbersOf(set) == "1/01/01/001");
    }

    [Fact]
    public void WhenAGroupedSetContainsIt_ThenTheNamedLicenceLosesItsSingleSet()
    {
        var licenceSets = new List<LicenceSet>
        {
            SingleSet("1/01/01/001"),
            GroupedSet("1/01/01/001", "1/01/01/002")
        };

        var result = LicenceSetHelper.WithRedundantSingleSetsRemoved(licenceSets, "1/01/01/001");

        Assert.Single(result);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, result[0].LicenceSetType);
    }

    [Fact]
    public void WhenASingleSetIsForAnotherLicence_ThenItIsStillRemoved()
    {
        var licenceSets = new List<LicenceSet>
        {
            SingleSet("1/01/01/001"),
            SingleSet("1/01/01/009"),
            GroupedSet("1/01/01/002", "1/01/01/003")
        };

        var result = LicenceSetHelper.WithRedundantSingleSetsRemoved(licenceSets, "1/01/01/001");

        Assert.Equal(2, result.Count);
        Assert.DoesNotContain(result, set => LicenceNumbersOf(set) == "1/01/01/009");
    }

    [Fact]
    public void WhenThereAreNoGroupedSets_ThenEverythingIsKept()
    {
        var licenceSets = new List<LicenceSet>
        {
            SingleSet("1/01/01/001"),
            SingleSet("1/01/01/009")
        };

        var result = LicenceSetHelper.WithRedundantSingleSetsRemoved(licenceSets, "1/01/01/001");

        Assert.Equal(2, result.Count);
    }

    // AddAdditionalLicenceSetsAsync is the call site that names no licence.
    [Fact]
    public void WhenNoLicenceIsNamed_ThenAllSingleSetsAreRemoved()
    {
        var licenceSets = new List<LicenceSet>
        {
            SingleSet("1/01/01/001"),
            GroupedSet("1/01/01/002", "1/01/01/003")
        };

        var result = LicenceSetHelper.WithRedundantSingleSetsRemoved(licenceSets, null);

        Assert.Single(result);
        Assert.Equal(LicenceSetType.LicencesGroupedByAbstractionLimits, result[0].LicenceSetType);
    }

    private static LicenceSet SingleSet(string licenceNumber)
    {
        return new LicenceSet
        {
            LicenceSetType = LicenceSetType.SingleLicenceOnly,
            Licences = [LicenceFor(licenceNumber)]
        };
    }

    private static LicenceSet GroupedSet(params string[] licenceNumbers)
    {
        return new LicenceSet
        {
            LicenceSetType = LicenceSetType.LicencesGroupedByAbstractionLimits,
            Licences = licenceNumbers.Select(LicenceFor).ToArray()
        };
    }

    private static Licence LicenceFor(string licenceNumber)
    {
        return new Licence
        {
            LicenceNumber = new ValueWithConfidence<string>(licenceNumber, 1, 1)
        };
    }

    private static string LicenceNumbersOf(LicenceSet licenceSet)
    {
        return string.Join(", ", licenceSet.Licences.Select(licence => licence.LicenceNumber?.Value));
    }
}
