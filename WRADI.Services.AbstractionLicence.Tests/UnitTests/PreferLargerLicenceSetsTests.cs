using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class PreferLargerLicenceSetsTests
{
    private const LicenceSetType Lineage = LicenceSetType.LicencesGroupedByLineage;
    private const LicenceSetType Limits = LicenceSetType.LicencesGroupedByAbstractionLimits;

    [Fact]
    public void WhenASetIsOfAnotherType_ThenItPassesThrough()
    {
        var other = SetOf(Limits, "1/2/3");

        var result = LicenceSetHelper.PreferLargerLicenceSets([other], Lineage);

        Assert.Same(other, Assert.Single(result));
    }

    [Fact]
    public void WhenTypesAreMixed_ThenOtherTypesComeFirst()
    {
        var considered = SetOf(Lineage, "1/2/3");
        var untouched = SetOf(Limits, "4/5/6");

        var result = LicenceSetHelper.PreferLargerLicenceSets(
            [considered, untouched], Lineage);

        Assert.Equal([untouched, considered], result);
    }

    [Fact]
    public void WhenALargerSetContainsAllOfASmallerOne_ThenOnlyTheLargerSurvives()
    {
        var smaller = SetOf(Lineage, "1/2/3");
        var larger = SetOf(Lineage, "1/2/3", "4/5/6");

        var result = LicenceSetHelper.PreferLargerLicenceSets(
            [smaller, larger], Lineage);

        Assert.Same(larger, Assert.Single(result));
    }

    [Fact]
    public void WhenTwoSetsAreTheSameSize_ThenBothSurvive()
    {
        var result = LicenceSetHelper.PreferLargerLicenceSets(
            [SetOf(Lineage, "1/2/3"), SetOf(Lineage, "4/5/6")], Lineage);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void WhenASetIsDropped_ThenItsLicencesNoLongerReferenceIt()
    {
        var smaller = SetOf(Lineage, "1/2/3");
        var droppedId = smaller.LicenceSetId;
        smaller.Licences[0].LicenceSets = [Reference(droppedId, Lineage)];

        LicenceSetHelper.PreferLargerLicenceSets(
            [smaller, SetOf(Lineage, "1/2/3", "4/5/6")], Lineage);

        Assert.Empty(smaller.Licences[0].LicenceSets);
    }

    // The filter names LicencesGroupedByLineage directly rather than the type it was asked about,
    // so a lineage reference is stripped even when another type is being considered.
    [Fact]
    public void WhenAnotherTypeIsConsidered_ThenLineageReferencesAreStillStripped()
    {
        var encompassing = SetOf(Limits, "1/2/3", "4/5/6");
        var licence = encompassing.Licences[0];
        licence.LicenceSets =
        [
            Reference(encompassing.LicenceSetId, Limits),
            Reference("HIS-some-lineage-set", Lineage),
            Reference("LIM-some-other-limits-set", Limits)
        ];

        LicenceSetHelper.PreferLargerLicenceSets(
            [encompassing, SetOf(Limits, "1/2/3")], Limits);

        Assert.Equal(
            [encompassing.LicenceSetId, "LIM-some-other-limits-set"],
            licence.LicenceSets.Select(ls => ls.LicenceSetId));
    }

    private static LicenceSetReference Reference(string? licenceSetId, LicenceSetType licenceSetType)
    {
        return new LicenceSetReference { LicenceSetId = licenceSetId, LicenceSetType = licenceSetType };
    }

    private static LicenceSet SetOf(LicenceSetType licenceSetType, params string[] licenceNumbers)
    {
        return new LicenceSet
        {
            LicenceSetType = licenceSetType,
            Licences = licenceNumbers
                .Select(licenceNumber => LicenceBuilder.A().Numbered(licenceNumber).Build())
                .ToArray()
        };
    }
}
