using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class AbstractionLimitShapeTests
{
    [Fact]
    public void WhenThereIsNoValue_ThenThereIsNoLimit()
    {
        Assert.Null(AbstractionLicenceSchemaConverter.GetAbstractionLimit(
            null, LimitPeriodType.PerDay, [], [], Contained()));
    }

    // The unit comes from the period alone, not from anything the document said: a rate is litres
    // and every other period is cubic metres.
    [Theory]
    [InlineData(LimitPeriodType.PerSecond, "litres")]
    [InlineData(LimitPeriodType.PerDay, "cubic metres")]
    [InlineData(LimitPeriodType.PerYear, "cubic metres")]
    [InlineData(LimitPeriodType.InTotal, "cubic metres")]
    public void TheUnitIsChosenByPeriod(LimitPeriodType periodType, string expectedUnits)
    {
        var limit = AbstractionLicenceSchemaConverter.GetAbstractionLimit(
            100, periodType, [], [], Contained());

        Assert.Equal(expectedUnits, limit!.Units);
    }

    [Fact]
    public void ThePointsPurposesAndProvenanceArePassedStraightThrough()
    {
        Point[] points = [new Point { Id = "P1" }];
        Purpose[] purposes = [new Purpose { Id = "U1" }];
        var containedIn = Contained();

        var limit = AbstractionLicenceSchemaConverter.GetAbstractionLimit(
            42, LimitPeriodType.PerDay, purposes, points, containedIn);

        Assert.Equal(42, limit!.Value);
        Assert.Same(points, limit.Points);
        Assert.Same(purposes, limit.Purposes);
        Assert.Same(containedIn, Assert.Single(limit.ContainedIn!));
    }

    // Points and purposes live either on the group or on its limits, never both: when the group
    // names any, the limits' own are dropped.
    [Fact]
    public void WhenTheGroupNamesPurposes_ThenTheLimitsLoseTheirs()
    {
        var limits = new List<AbstractionLimit> { LimitWith(purposes: [new Purpose { Id = "U1" }]) };
        var group = new AbstractionLimitGroup { Purposes = [new Purpose { Id = "U2" }], Limits = limits };

        AbstractionLicenceSchemaConverter.NullOutLimitLevelPointsAndPurposesIfRelevant(group, limits);

        Assert.Null(limits[0].Purposes);
        Assert.NotNull(group.Purposes);
    }

    [Fact]
    public void WhenTheGroupNamesPoints_ThenTheLimitsLoseTheirs()
    {
        var limits = new List<AbstractionLimit> { LimitWith(points: [new Point { Id = "P1" }]) };
        var group = new AbstractionLimitGroup { Points = [new Point { Id = "P2" }], Limits = limits };

        AbstractionLicenceSchemaConverter.NullOutLimitLevelPointsAndPurposesIfRelevant(group, limits);

        Assert.Null(limits[0].Points);
        Assert.NotNull(group.Points);
    }

    // An empty array means the group named none, which is normalised to null so later code has
    // one "absent" to test rather than two.
    [Fact]
    public void WhenTheGroupNamesAnEmptySet_ThenItBecomesNullAndTheLimitsKeepTheirs()
    {
        var limits = new List<AbstractionLimit>
        {
            LimitWith(points: [new Point { Id = "P1" }], purposes: [new Purpose { Id = "U1" }])
        };

        var group = new AbstractionLimitGroup { Points = [], Purposes = [], Limits = limits };

        AbstractionLicenceSchemaConverter.NullOutLimitLevelPointsAndPurposesIfRelevant(group, limits);

        Assert.Null(group.Points);
        Assert.Null(group.Purposes);
        Assert.NotNull(limits[0].Points);
        Assert.NotNull(limits[0].Purposes);
    }

    [Fact]
    public void WhenTheGroupNamesNothingAtAll_ThenNothingIsChanged()
    {
        var limits = new List<AbstractionLimit>
        {
            LimitWith(points: [new Point { Id = "P1" }], purposes: [new Purpose { Id = "U1" }])
        };

        var group = new AbstractionLimitGroup { Limits = limits };

        AbstractionLicenceSchemaConverter.NullOutLimitLevelPointsAndPurposesIfRelevant(group, limits);

        Assert.Null(group.Points);
        Assert.NotNull(limits[0].Points);
        Assert.NotNull(limits[0].Purposes);
    }

    private static AbstractionLimit LimitWith(Point[]? points = null, Purpose[]? purposes = null)
    {
        return new AbstractionLimit
        {
            Value = 100,
            Units = "litres",
            PeriodType = LimitPeriodType.PerDay,
            Points = points,
            Purposes = purposes
        };
    }

    private static ContainedInInformation Contained()
    {
        return new ContainedInInformation { SectionName = "AbstractionLimits" };
    }
}
