using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class AbstractionLimitComparisonTests
{
    [Fact]
    public void WhenThePeriodAndTheConvertedValueMatch_ThenTheLimitsAreEqual()
    {
        Assert.True(AbstractionLimitHelper.AreLimitsEqual(
            AbstractionLimitBuilder.A().Of(1_000, "gallons").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(4_546.09, "litres").Per(LimitPeriodType.PerDay).Build()));
    }

    [Fact]
    public void WhenOnlyThePeriodDiffers_ThenTheLimitsAreNotEqual()
    {
        Assert.False(AbstractionLimitHelper.AreLimitsEqual(
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerYear).Build()));
    }

    // AreValuesEqual allows 0.75% between the document and NALD.
    [Theory]
    [InlineData(100, 100.5, true)]
    [InlineData(100, 101, false)]
    public void ValuesWithinTheToleranceCount(double left, double right, bool expected)
    {
        Assert.Equal(expected, AbstractionLimitHelper.AreLimitsEqual(
            AbstractionLimitBuilder.A().Of(left, "litres").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(right, "litres").Per(LimitPeriodType.PerDay).Build()));
    }

    // AreValuesEqual divides by the left value, so a zero never compares equal - not even to
    // another zero, which yields NaN and fails the range check.
    [Fact]
    public void WhenBothValuesAreZero_ThenTheLimitsAreNotEqual()
    {
        Assert.False(AbstractionLimitHelper.AreLimitsEqual(
            AbstractionLimitBuilder.A().Of(0, "litres").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(0, "litres").Per(LimitPeriodType.PerDay).Build()));
    }

    [Fact]
    public void WhenEitherValueIsMissing_ThenTheLimitsAreNotEqual()
    {
        Assert.False(AbstractionLimitHelper.AreLimitsEqual(
            AbstractionLimitBuilder.A().Of(null, null).Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(100, null).Per(LimitPeriodType.PerDay).Build()));
    }

    // No units means the number is taken as litres rather than refused.
    [Fact]
    public void WhenUnitsAreMissing_ThenTheValueIsComparedAsIs()
    {
        Assert.True(AbstractionLimitHelper.AreLimitsEqual(
            AbstractionLimitBuilder.A().Of(100, null).Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build()));
    }

    // Line grouping can weld a unit to the next word, and the conversion refuses it rather than
    // returning no answer, so the comparison throws instead of reporting not-equal.
    [Fact]
    public void WhenAUnitCannotBeConverted_ThenComparingThrows()
    {
        Assert.ThrowsAny<Exception>(() => AbstractionLimitHelper.AreLimitsEqual(
            AbstractionLimitBuilder.A().Of(100, "acre feet").Per(LimitPeriodType.PerDay).Build(),
            AbstractionLimitBuilder.A().Of(100, "litres").Per(LimitPeriodType.PerDay).Build()));
    }

    [Fact]
    public void WhenTheGroupHoldsAMatchingLimit_ThenItIsFound()
    {
        var group = new AbstractionLimitGroup
        {
            Limits = [AbstractionLimitBuilder.A().Of(50, "litres").Per(LimitPeriodType.PerDay).Build(), AbstractionLimitBuilder.A().Of(1_000, "gallons").Per(LimitPeriodType.PerYear).Build()]
        };

        Assert.True(AbstractionLimitHelper.GroupContainsLimit(
            group, AbstractionLimitBuilder.A().Of(4_546.09, "litres").Per(LimitPeriodType.PerYear).Build()));
    }

    [Fact]
    public void WhenTheGroupIsEmpty_ThenNothingIsFound()
    {
        Assert.False(AbstractionLimitHelper.GroupContainsLimit(
            new AbstractionLimitGroup(), AbstractionLimitBuilder.A().Of(50, "litres").Per(LimitPeriodType.PerDay).Build()));
    }

}
