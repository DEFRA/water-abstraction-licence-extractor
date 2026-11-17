using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class RemoveDuplicateLimitsPerGroupTests
{
    [Fact]
    public void WhenTwoLimitsAreIdentical_ThenOneIsKept()
    {
        var group = AbstractionLimitBuilder.GroupOf(
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build());

        AbstractionLimitHelper.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Single(group.Limits);
    }

    [Fact]
    public void WhenTheLimitsDiffer_ThenBothAreKept()
    {
        var group = AbstractionLimitBuilder.GroupOf(
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(200, "litres").Per(LimitPeriodType.PerDay).Build());

        AbstractionLimitHelper.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    // The key holds Units as written, not converted, so the same quantity in two units survives
    // twice - unlike AreLimitsEqual, which compares in litres and would call these equal.
    [Fact]
    public void WhenTheSameQuantityIsWrittenInDifferentUnits_ThenBothAreKept()
    {
        var group = AbstractionLimitBuilder.GroupOf(
            AbstractionLimitBuilder.A().Of(1_000, "gallons").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(4_546.09, "litres").Per(LimitPeriodType.PerDay).Build());

        AbstractionLimitHelper.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    // The grouping key projects Points and Purposes with Select(...).ToArray(), so each limit gets
    // a fresh array, and arrays compare by reference. Any limit carrying points or purposes
    // therefore has a key no other limit can match, and is never de-duplicated - sharing one
    // source array does not help, because the projection copies it either way.
    [Fact]
    public void WhenIdenticalLimitsCarryPoints_ThenNeitherIsRemoved()
    {
        var group = AbstractionLimitBuilder.GroupOf(
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).At("P1").Build(),
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).At("P1").Build());

        AbstractionLimitHelper.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    [Fact]
    public void WhenIdenticalLimitsShareOnePointArray_ThenNeitherIsRemovedEither()
    {
        Point[] shared = [new Point { Id = "P1", NaldId = "N1" }];

        var group = AbstractionLimitBuilder.GroupOf(
            new AbstractionLimit { Value = 100, Units = "litres", PeriodType = LimitPeriodType.PerDay, Points = shared },
            new AbstractionLimit { Value = 100, Units = "litres", PeriodType = LimitPeriodType.PerDay, Points = shared });

        AbstractionLimitHelper.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    [Fact]
    public void EachGroupIsDeDupedOnItsOwn()
    {
        var first = AbstractionLimitBuilder.GroupOf(
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build());

        var second = AbstractionLimitBuilder.GroupOf(AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build());

        AbstractionLimitHelper.RemoveDuplicateLimitsPerGroup([first, second]);

        Assert.Single(first.Limits);
        Assert.Single(second.Limits);
    }



}
