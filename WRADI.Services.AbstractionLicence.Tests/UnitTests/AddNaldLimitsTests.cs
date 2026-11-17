using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

// A NALD limit that matches one already extracted from the document adds NALD as another source
// for it; one that matches nothing becomes a limit in its own right.
public class AddNaldLimitsTests
{
    [Fact]
    public void WhenTheGroupCarriesProvenance_ThenNaldIsAddedAtTheGroupLevel()
    {
        var group = GroupOf(Document(), Limit(100, "litres"));
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLicenceSchemaConverter.AddNaldLimits([Limit(100, "litres")], [group], ref individuals);

        Assert.Contains(group.ContainedIn!, ci => ci.Source == InformationSource.Nald);
        Assert.Empty(individuals);
    }

    [Fact]
    public void WhenNaldIsAlreadyNamedOnTheGroup_ThenItIsNotAddedTwice()
    {
        var group = GroupOf(Document(), Limit(100, "litres"));
        group.ContainedIn = [new ContainedInInformation { Source = InformationSource.Nald }];
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLicenceSchemaConverter.AddNaldLimits([Limit(100, "litres")], [group], ref individuals);

        Assert.Single(group.ContainedIn!);
    }

    // With no provenance on the group, each limit in it is marked instead.
    [Fact]
    public void WhenTheGroupCarriesNone_ThenNaldIsAddedToEveryLimit()
    {
        var limit = Limit(100, "litres");
        limit.ContainedIn = [Document()];

        var group = new AbstractionLimitGroup { Limits = [limit] };
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLicenceSchemaConverter.AddNaldLimits([Limit(100, "litres")], [group], ref individuals);

        Assert.Contains(limit.ContainedIn!, ci => ci.Source == InformationSource.Nald);
    }

    // The limit's own provenance is read with a null-forgiving operator, so a limit that has none
    // throws rather than being given NALD as its first source.
    [Fact]
    public void WhenNeitherTheGroupNorItsLimitCarriesProvenance_ThenItThrows()
    {
        var group = new AbstractionLimitGroup { Limits = [Limit(100, "litres")] };
        var individuals = new List<AbstractionLimitGroup>();

        Assert.Throws<ArgumentNullException>(() =>
            AbstractionLicenceSchemaConverter.AddNaldLimits([Limit(100, "litres")], [group], ref individuals));
    }

    // Matching converts first, so the same quantity written in another unit still counts as found.
    [Fact]
    public void TheMatchIsMadeInBaseUnits()
    {
        var group = GroupOf(Document(), Limit(4_546.09, "litres"));
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLicenceSchemaConverter.AddNaldLimits([Limit(1_000, "gallons")], [group], ref individuals);

        Assert.Contains(group.ContainedIn!, ci => ci.Source == InformationSource.Nald);
        Assert.Empty(individuals);
    }

    [Fact]
    public void WhenNothingMatches_ThenTheNaldLimitBecomesANewGroup()
    {
        var group = GroupOf(Document(), Limit(100, "litres"));
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLicenceSchemaConverter.AddNaldLimits([Limit(999, "litres")], [group], ref individuals);

        var added = Assert.Single(individuals);
        Assert.Equal(999, Assert.Single(added.Limits).Value);
    }

    // Every unmatched limit goes into one group whatever its period, so a daily and an annual
    // limit end up grouped together.
    [Fact]
    public void AllUnmatchedLimitsShareASingleNewGroup()
    {
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLicenceSchemaConverter.AddNaldLimits(
            [Limit(10, "litres"), Limit(20, "litres", LimitPeriodType.PerYear)],
            [],
            ref individuals);

        Assert.Equal(2, Assert.Single(individuals).Limits.Count);
    }

    [Fact]
    public void WhenThereAreNoNaldLimits_ThenNoGroupIsAdded()
    {
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLicenceSchemaConverter.AddNaldLimits([], [], ref individuals);

        Assert.Empty(individuals);
    }

    // A unit the conversion cannot read fails the whole merge, not just the limit carrying it.
    [Fact]
    public void WhenAnExistingLimitHasAnUnreadableUnit_ThenTheMergeThrows()
    {
        var group = GroupOf(Document(), Limit(100, "acre feet"));
        var individuals = new List<AbstractionLimitGroup>();

        Assert.ThrowsAny<Exception>(() =>
            AbstractionLicenceSchemaConverter.AddNaldLimits([Limit(100, "litres")], [group], ref individuals));
    }

    private static AbstractionLimitGroup GroupOf(ContainedInInformation containedIn, params AbstractionLimit[] limits)
    {
        return new AbstractionLimitGroup { ContainedIn = [containedIn], Limits = limits.ToList() };
    }

    private static AbstractionLimit Limit(
        double value, string units, LimitPeriodType periodType = LimitPeriodType.PerDay)
    {
        return new AbstractionLimit { Value = value, Units = units, PeriodType = periodType };
    }

    private static ContainedInInformation Document()
    {
        return new ContainedInInformation { Source = InformationSource.Document };
    }
}
