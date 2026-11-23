using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class AsDistinctLicenceSetsTests
{
    [Fact]
    public void WhenGroupsHoldDifferentSets_ThenAllAreFlattenedInGroupOrder()
    {
        var result = LicenceSetHelper.AsDistinctLicenceSets(
            [[SetOf("1/2/3"), SetOf("4/5/6")], [SetOf("7/8/9")]]);

        Assert.Equal(["SNG123", "SNG456", "SNG789"], result.Select(ls => ls.LicenceSetId));
    }

    [Fact]
    public void WhenTheSameIdAppearsInTwoGroups_ThenTheFirstOccurrenceIsKept()
    {
        var first = SetOf("1/2/3");

        var result = LicenceSetHelper.AsDistinctLicenceSets(
            [[first], [SetOf("1/2/3")]]);

        Assert.Same(first, Assert.Single(result));
    }

    [Fact]
    public void WhenTheSameIdAppearsTwiceInOneGroup_ThenItIsStillDeduplicated()
    {
        var result = LicenceSetHelper.AsDistinctLicenceSets(
            [[SetOf("1/2/3"), SetOf("1/2/3")]]);

        Assert.Single(result);
    }

    [Fact]
    public void WhenTwoSetsHoldNoLicences_ThenOnlyTheFirstSurvives()
    {
        var result = LicenceSetHelper.AsDistinctLicenceSets([[SetOf(), SetOf()]]);

        Assert.Equal(["SNG"], result.Select(ls => ls.LicenceSetId));
    }

    [Fact]
    public void WhenTwoSetsHoldTheSameLicenceButDifferentTypes_ThenBothSurvive()
    {
        var result = LicenceSetHelper.AsDistinctLicenceSets(
            [[SetOf("1/2/3"), SetOf(LicenceSetType.LicencesGroupedByLineage, "1/2/3")]]);

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public void WhenAGroupIsEmpty_ThenItContributesNothing()
    {
        var result = LicenceSetHelper.AsDistinctLicenceSets(
            [[], [SetOf("1/2/3")], []]);

        Assert.Equal(["SNG123"], result.Select(ls => ls.LicenceSetId));
    }

    [Fact]
    public void WhenThereAreNoGroups_ThenTheResultIsEmpty()
    {
        Assert.Empty(LicenceSetHelper.AsDistinctLicenceSets([]));
    }

    private static LicenceSet SetOf(params string[] licenceNumbers)
    {
        return SetOf(LicenceSetType.SingleLicenceOnly, licenceNumbers);
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
