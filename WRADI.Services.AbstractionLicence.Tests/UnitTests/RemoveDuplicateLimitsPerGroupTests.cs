using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class RemoveDuplicateLimitsPerGroupTests
{
    [Fact]
    public void WhenTwoLimitsAreIdentical_ThenOneIsKept()
    {
        var group = GroupOf(
            Limit(100, "litres", LimitPeriodType.PerDay),
            Limit(100, "litres", LimitPeriodType.PerDay));

        AbstractionLicenceSchemaConverter.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Single(group.Limits);
    }

    [Fact]
    public void WhenTheLimitsDiffer_ThenBothAreKept()
    {
        var group = GroupOf(
            Limit(100, "litres", LimitPeriodType.PerDay),
            Limit(200, "litres", LimitPeriodType.PerDay));

        AbstractionLicenceSchemaConverter.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    // The key holds Units as written, not converted, so the same quantity in two units survives
    // twice - unlike AreLimitsEqual, which compares in litres and would call these equal.
    [Fact]
    public void WhenTheSameQuantityIsWrittenInDifferentUnits_ThenBothAreKept()
    {
        var group = GroupOf(
            Limit(1_000, "gallons", LimitPeriodType.PerDay),
            Limit(4_546.09, "litres", LimitPeriodType.PerDay));

        AbstractionLicenceSchemaConverter.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    // The grouping key projects Points and Purposes with Select(...).ToArray(), so each limit gets
    // a fresh array, and arrays compare by reference. Any limit carrying points or purposes
    // therefore has a key no other limit can match, and is never de-duplicated - sharing one
    // source array does not help, because the projection copies it either way.
    [Fact]
    public void WhenIdenticalLimitsCarryPoints_ThenNeitherIsRemoved()
    {
        var group = GroupOf(
            LimitWithPoint(100, "litres", LimitPeriodType.PerDay, "P1", "N1"),
            LimitWithPoint(100, "litres", LimitPeriodType.PerDay, "P1", "N1"));

        AbstractionLicenceSchemaConverter.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    [Fact]
    public void WhenIdenticalLimitsShareOnePointArray_ThenNeitherIsRemovedEither()
    {
        Point[] shared = [new Point { Id = "P1", NaldId = "N1" }];

        var group = GroupOf(
            new AbstractionLimit { Value = 100, Units = "litres", PeriodType = LimitPeriodType.PerDay, Points = shared },
            new AbstractionLimit { Value = 100, Units = "litres", PeriodType = LimitPeriodType.PerDay, Points = shared });

        AbstractionLicenceSchemaConverter.RemoveDuplicateLimitsPerGroup([group]);

        Assert.Equal(2, group.Limits.Count);
    }

    [Fact]
    public void EachGroupIsDeDupedOnItsOwn()
    {
        var first = GroupOf(
            Limit(100, "litres", LimitPeriodType.PerDay),
            Limit(100, "litres", LimitPeriodType.PerDay));

        var second = GroupOf(Limit(100, "litres", LimitPeriodType.PerDay));

        AbstractionLicenceSchemaConverter.RemoveDuplicateLimitsPerGroup([first, second]);

        Assert.Single(first.Limits);
        Assert.Single(second.Limits);
    }

    private static AbstractionLimitGroup GroupOf(params AbstractionLimit[] limits)
    {
        return new AbstractionLimitGroup { Limits = limits.ToList() };
    }

    private static AbstractionLimit Limit(double? value, string? units, LimitPeriodType periodType)
    {
        return new AbstractionLimit { Value = value, Units = units, PeriodType = periodType };
    }

    private static AbstractionLimit LimitWithPoint(
        double? value, string? units, LimitPeriodType periodType, string id, string naldId)
    {
        return new AbstractionLimit
        {
            Value = value,
            Units = units,
            PeriodType = periodType,
            Points = [new Point { Id = id, NaldId = naldId }]
        };
    }
}
