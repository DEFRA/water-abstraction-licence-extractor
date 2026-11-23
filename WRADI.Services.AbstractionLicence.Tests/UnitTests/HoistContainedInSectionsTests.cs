using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class HoistContainedInSectionsTests
{
    [Fact]
    public void WhenEveryLimitSharesItsProvenance_ThenItMovesToTheGroupAndLeavesTheLimits()
    {
        var group = AbstractionLimitBuilder.GroupOf(LimitSeenIn(Nald("Quantity")), LimitSeenIn(Nald("Quantity")));

        AbstractionLimitHelper.HoistContainedInSections([group], []);

        Assert.Equal("Quantity", Assert.Single(group.ContainedIn!).SectionName);
        Assert.All(group.Limits, limit => Assert.Null(limit.ContainedIn));
    }

    [Fact]
    public void WhenLimitsDisagree_ThenTheGroupIsMarkedMixedAndTheLimitsKeepTheirs()
    {
        var group = AbstractionLimitBuilder.GroupOf(LimitSeenIn(Nald("Quantity")), LimitSeenIn(Nald("Conditions")));

        AbstractionLimitHelper.HoistContainedInSections([group], []);

        Assert.Equal(
            InformationSource.MixedSourcesOrMixedReasons,
            Assert.Single(group.ContainedIn!).Source);

        Assert.All(group.Limits, limit => Assert.NotNull(limit.ContainedIn));
    }

    [Fact]
    public void WhenThereIsOneLimit_ThenItsProvenanceIsHoisted()
    {
        var group = AbstractionLimitBuilder.GroupOf(LimitSeenIn(Nald("Quantity")));

        AbstractionLimitHelper.HoistContainedInSections([group], []);

        Assert.Equal("Quantity", Assert.Single(group.ContainedIn!).SectionName);
    }

    [Fact]
    public void WhenAggregatesArePassed_ThenTheyAreHoistedToo()
    {
        var aggregate = new Aggregate { Limits = [LimitSeenIn(Nald("Quantity"))] };

        AbstractionLimitHelper.HoistContainedInSections([], [aggregate]);

        Assert.Equal("Quantity", Assert.Single(aggregate.ContainedIn!).SectionName);
    }

    [Fact]
    public void WhenLimitsDifferOnlyByPageNumber_ThenTheyCountAsTheSame()
    {
        var group = AbstractionLimitBuilder.GroupOf(
            LimitSeenIn(new ContainedInInformation { Source = InformationSource.Nald, SectionName = "Quantity", PageNumber = 1 }),
            LimitSeenIn(new ContainedInInformation { Source = InformationSource.Nald, SectionName = "Quantity", PageNumber = 9 }));

        AbstractionLimitHelper.HoistContainedInSections([group], []);

        Assert.Equal("Quantity", Assert.Single(group.ContainedIn!).SectionName);
    }

    [Fact]
    public void WhenTwoLimitsHoldTheSameEntriesInADifferentOrder_ThenTheGroupIsMarkedMixed()
    {
        var group = AbstractionLimitBuilder.GroupOf(
            LimitSeenIn(Nald("Quantity"), Nald("Conditions")),
            LimitSeenIn(Nald("Conditions"), Nald("Quantity")));

        AbstractionLimitHelper.HoistContainedInSections([group], []);

        Assert.Equal(
            InformationSource.MixedSourcesOrMixedReasons,
            Assert.Single(group.ContainedIn!).Source);
    }

    [Fact]
    public void WhenAGroupHoldsNoLimits_ThenItThrows()
    {
        Assert.Throws<InvalidOperationException>(() =>
            AbstractionLimitHelper.HoistContainedInSections([new AbstractionLimitGroup()], []));
    }

    private static ContainedInInformation Nald(string sectionName)
    {
        return new ContainedInInformation { Source = InformationSource.Nald, SectionName = sectionName };
    }

    private static AbstractionLimit LimitSeenIn(params ContainedInInformation[] containedIn)
    {
        return AbstractionLimitBuilder.A().SeenIn(containedIn).Build();
    }

}
