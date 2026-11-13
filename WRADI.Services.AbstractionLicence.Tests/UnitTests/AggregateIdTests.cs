using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Aggregate.Id is what DeDuplicateAggregates dedupes on and AssertNoDuplicateAggregates groups
/// by, so two aggregates sharing an Id are treated as one. These state current behaviour, not
/// desired behaviour.
/// </summary>
public class AggregateIdTests
{
    private static Aggregate Aggregate(string sourceLicence, string[] linked, params double[] limits) =>
        new()
        {
            SourceLicenceNumber = sourceLicence,
            PrimaryType = PrimaryType.LicenceToLicence,
            SubType = SubType.PointToPoint,
            LinkedLicences = linked,
            Limits = limits.Select(value => new AbstractionLimit { Value = value }).ToList()
        };

    [Fact]
    public void WhenTheSourceAndLinksAndTotalMatch_ThenTheIdMatches()
    {
        Assert.Equal(
            Aggregate("1/2/3", ["4/5/6"], 100).Id,
            Aggregate("1/2/3", ["4/5/6"], 100).Id);
    }

    // Separators are stripped, so the same licence written either way is one aggregate.
    [Fact]
    public void WhenTheSourceIsWrittenWithDifferentSeparators_ThenTheIdStillMatches()
    {
        Assert.Equal(
            Aggregate("1/2/3", [], 100).Id,
            Aggregate("1 2 3", [], 100).Id);
    }

    // Linked licences are ordered before being appended, so the order they were found in does not
    // produce a different aggregate.
    [Fact]
    public void WhenLinkedLicencesArriveInADifferentOrder_ThenTheIdStillMatches()
    {
        Assert.Equal(
            Aggregate("1/2/3", ["4/5/6", "7/8/9"], 100).Id,
            Aggregate("1/2/3", ["7/8/9", "4/5/6"], 100).Id);
    }

    [Fact]
    public void WhenTheCombinedLimitDiffers_ThenTheIdDiffers()
    {
        Assert.NotEqual(
            Aggregate("1/2/3", [], 100).Id,
            Aggregate("1/2/3", [], 200).Id);
    }

    // WRADI-547. GetCombinedLimitValue accumulates in a double and returns an unchecked (int)
    // cast, which saturates at int.MaxValue. Every aggregate above that total reports the same
    // value, so distinct aggregates collapse onto one Id - and Id is what the dedup and the
    // duplicate assertion work from.
    [Fact]
    public void WhenTheCombinedLimitExceedsIntMax_ThenDistinctAggregatesShareAnId()
    {
        var fiveBillion = Aggregate("1/2/3", [], 5_000_000_000);
        var nineBillion = Aggregate("1/2/3", [], 9_000_000_000);

        Assert.Equal(int.MaxValue, fiveBillion.GetCombinedLimitValue());
        Assert.Equal(int.MaxValue, nineBillion.GetCombinedLimitValue());
        Assert.Equal(fiveBillion.Id, nineBillion.Id);
    }

    // The same overflow is loud rather than silent once the values are summed across aggregates,
    // which is the path GetSupersets takes.
    [Fact]
    public void WhenCombinedLimitsAreSummedAcrossAggregates_ThenTheOverflowThrows()
    {
        var aggregates = new[]
        {
            Aggregate("1/2/3", [], 5_000_000_000),
            Aggregate("4/5/6", [], 9_000_000_000)
        };

        Assert.Throws<OverflowException>(() => aggregates.Sum(a => a.GetCombinedLimitValue()));
    }

    [Fact]
    public void WhenLimitValuesAreMissing_ThenTheyAreSkippedRatherThanCountedAsZero()
    {
        var withNull = new Aggregate
        {
            SourceLicenceNumber = "1/2/3",
            PrimaryType = PrimaryType.LicenceToLicence,
            SubType = SubType.PointToPoint,
            LinkedLicences = [],
            Limits = [new AbstractionLimit { Value = 100 }, new AbstractionLimit { Value = null }]
        };

        Assert.Equal(100, withNull.GetCombinedLimitValue());
    }
}
