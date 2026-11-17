using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class AbstractionLimitComparisonTests
{
    [Fact]
    public void WhenThePeriodAndTheConvertedValueMatch_ThenTheLimitsAreEqual()
    {
        Assert.True(AbstractionLimitHelper.AreLimitsEqual(
            Limit(1_000, "gallons", LimitPeriodType.PerDay),
            Limit(4_546.09, "litres", LimitPeriodType.PerDay)));
    }

    [Fact]
    public void WhenOnlyThePeriodDiffers_ThenTheLimitsAreNotEqual()
    {
        Assert.False(AbstractionLimitHelper.AreLimitsEqual(
            Limit(100, "litres", LimitPeriodType.PerDay),
            Limit(100, "litres", LimitPeriodType.PerYear)));
    }

    // AreValuesEqual allows 0.75% between the document and NALD.
    [Theory]
    [InlineData(100, 100.5, true)]
    [InlineData(100, 101, false)]
    public void ValuesWithinTheToleranceCount(double left, double right, bool expected)
    {
        Assert.Equal(expected, AbstractionLimitHelper.AreLimitsEqual(
            Limit(left, "litres", LimitPeriodType.PerDay),
            Limit(right, "litres", LimitPeriodType.PerDay)));
    }

    // AreValuesEqual divides by the left value, so a zero never compares equal - not even to
    // another zero, which yields NaN and fails the range check.
    [Fact]
    public void WhenBothValuesAreZero_ThenTheLimitsAreNotEqual()
    {
        Assert.False(AbstractionLimitHelper.AreLimitsEqual(
            Limit(0, "litres", LimitPeriodType.PerDay),
            Limit(0, "litres", LimitPeriodType.PerDay)));
    }

    [Fact]
    public void WhenEitherValueIsMissing_ThenTheLimitsAreNotEqual()
    {
        Assert.False(AbstractionLimitHelper.AreLimitsEqual(
            Limit(null, null, LimitPeriodType.PerDay),
            Limit(100, null, LimitPeriodType.PerDay)));
    }

    // No units means the number is taken as litres rather than refused.
    [Fact]
    public void WhenUnitsAreMissing_ThenTheValueIsComparedAsIs()
    {
        Assert.True(AbstractionLimitHelper.AreLimitsEqual(
            Limit(100, null, LimitPeriodType.PerDay),
            Limit(100, "litres", LimitPeriodType.PerDay)));
    }

    // Line grouping can weld a unit to the next word, and the conversion refuses it rather than
    // returning no answer - so one unreadable unit fails the comparison for the whole document.
    [Fact]
    public void WhenAUnitCannotBeConverted_ThenComparingThrows()
    {
        Assert.ThrowsAny<Exception>(() => AbstractionLimitHelper.AreLimitsEqual(
            Limit(100, "acre feet", LimitPeriodType.PerDay),
            Limit(100, "litres", LimitPeriodType.PerDay)));
    }

    [Fact]
    public void WhenTheGroupHoldsAMatchingLimit_ThenItIsFound()
    {
        var group = new AbstractionLimitGroup
        {
            Limits = [Limit(50, "litres", LimitPeriodType.PerDay), Limit(1_000, "gallons", LimitPeriodType.PerYear)]
        };

        Assert.True(AbstractionLimitHelper.GroupContainsLimit(
            group, Limit(4_546.09, "litres", LimitPeriodType.PerYear)));
    }

    [Fact]
    public void WhenTheGroupIsEmpty_ThenNothingIsFound()
    {
        Assert.False(AbstractionLimitHelper.GroupContainsLimit(
            new AbstractionLimitGroup(), Limit(50, "litres", LimitPeriodType.PerDay)));
    }

    private static AbstractionLimit Limit(double? value, string? units, LimitPeriodType periodType)
    {
        return new AbstractionLimit { Value = value, Units = units, PeriodType = periodType };
    }
}
