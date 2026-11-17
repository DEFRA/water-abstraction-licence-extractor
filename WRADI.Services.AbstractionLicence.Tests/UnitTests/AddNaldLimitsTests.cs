using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

// A NALD limit that matches one already extracted from the document adds NALD as another source
// for it; one that matches nothing becomes a limit in its own right.
public class AddNaldLimitsTests
{
    [Fact]
    public void WhenTheGroupCarriesProvenance_ThenNaldIsAddedAtTheGroupLevel()
    {
        var group = AbstractionLimitBuilder.GroupSeenIn(Document(), AbstractionLimitBuilder.A().Of(100, "litres").Build());
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLimitHelper.AddNaldLimits([AbstractionLimitBuilder.A().Of(100, "litres").Build()], [group], ref individuals);

        Assert.Contains(group.ContainedIn!, ci => ci.Source == InformationSource.Nald);
        Assert.Empty(individuals);
    }

    [Fact]
    public void WhenNaldIsAlreadyNamedOnTheGroup_ThenItIsNotAddedTwice()
    {
        var group = AbstractionLimitBuilder.GroupSeenIn(Document(), AbstractionLimitBuilder.A().Of(100, "litres").Build());
        group.ContainedIn = [new ContainedInInformation { Source = InformationSource.Nald }];
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLimitHelper.AddNaldLimits([AbstractionLimitBuilder.A().Of(100, "litres").Build()], [group], ref individuals);

        Assert.Single(group.ContainedIn!);
    }

    // With no provenance on the group, each limit in it is marked instead.
    [Fact]
    public void WhenTheGroupCarriesNone_ThenNaldIsAddedToEveryLimit()
    {
        var limit = AbstractionLimitBuilder.A().Of(100, "litres").Build();
        limit.ContainedIn = [Document()];

        var group = new AbstractionLimitGroup { Limits = [limit] };
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLimitHelper.AddNaldLimits([AbstractionLimitBuilder.A().Of(100, "litres").Build()], [group], ref individuals);

        Assert.Contains(limit.ContainedIn!, ci => ci.Source == InformationSource.Nald);
    }

    // The limit's own provenance is read with a null-forgiving operator, so a limit that has none
    // throws rather than being given NALD as its first source.
    [Fact]
    public void WhenNeitherTheGroupNorItsLimitCarriesProvenance_ThenItThrows()
    {
        var group = new AbstractionLimitGroup { Limits = [AbstractionLimitBuilder.A().Of(100, "litres").Build()] };
        var individuals = new List<AbstractionLimitGroup>();

        Assert.Throws<ArgumentNullException>(() =>
            AbstractionLimitHelper.AddNaldLimits([AbstractionLimitBuilder.A().Of(100, "litres").Build()], [group], ref individuals));
    }

    // Matching converts first, so the same quantity written in another unit still counts as found.
    [Fact]
    public void TheMatchIsMadeInBaseUnits()
    {
        var group = AbstractionLimitBuilder.GroupSeenIn(Document(), AbstractionLimitBuilder.A().Of(4_546.09, "litres").Build());
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLimitHelper.AddNaldLimits([AbstractionLimitBuilder.A().Of(1_000, "gallons").Build()], [group], ref individuals);

        Assert.Contains(group.ContainedIn!, ci => ci.Source == InformationSource.Nald);
        Assert.Empty(individuals);
    }

    [Fact]
    public void WhenNothingMatches_ThenTheNaldLimitBecomesANewGroup()
    {
        var group = AbstractionLimitBuilder.GroupSeenIn(Document(), AbstractionLimitBuilder.A().Of(100, "litres").Build());
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLimitHelper.AddNaldLimits([AbstractionLimitBuilder.A().Of(999, "litres").Build()], [group], ref individuals);

        var added = Assert.Single(individuals);
        Assert.Equal(999, Assert.Single(added.Limits).Value);
    }

    // Every unmatched limit goes into one group whatever its period, so a daily and an annual
    // limit end up grouped together.
    [Fact]
    public void AllUnmatchedLimitsShareASingleNewGroup()
    {
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLimitHelper.AddNaldLimits(
            [AbstractionLimitBuilder.A().Of(10, "litres").Build(), AbstractionLimitBuilder.A().Of(20, "litres").Per(LimitPeriodType.PerYear).Build()],
            [],
            ref individuals);

        Assert.Equal(2, Assert.Single(individuals).Limits.Count);
    }

    [Fact]
    public void WhenThereAreNoNaldLimits_ThenNoGroupIsAdded()
    {
        var individuals = new List<AbstractionLimitGroup>();

        AbstractionLimitHelper.AddNaldLimits([], [], ref individuals);

        Assert.Empty(individuals);
    }

    // A unit the conversion cannot read fails the whole merge, not just the limit carrying it.
    [Fact]
    public void WhenAnExistingLimitHasAnUnreadableUnit_ThenTheMergeThrows()
    {
        var group = AbstractionLimitBuilder.GroupSeenIn(Document(), AbstractionLimitBuilder.A().Of(100, "acre feet").Build());
        var individuals = new List<AbstractionLimitGroup>();

        Assert.ThrowsAny<Exception>(() =>
            AbstractionLimitHelper.AddNaldLimits([AbstractionLimitBuilder.A().Of(100, "litres").Build()], [group], ref individuals));
    }



    private static ContainedInInformation Document()
    {
        return new ContainedInInformation { Source = InformationSource.Document };
    }
}
