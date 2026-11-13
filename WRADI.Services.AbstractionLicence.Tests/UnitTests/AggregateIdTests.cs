using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Aggregate.Id is what DeDuplicateAggregates dedupes on and AssertNoDuplicateAggregates groups
/// by, so two aggregates sharing an Id are treated as one. These state current behaviour, not
/// desired behaviour.
/// </summary>
public class AggregateIdTests
{
    [Fact]
    public void WhenTheSourceAndLinksAndTotalMatch_ThenTheIdMatches()
    {
        Assert.Equal(
            AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Limited(100).Build().Id,
            AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Limited(100).Build().Id);
    }

    // Separators are stripped, so the same licence written either way is one aggregate.
    [Fact]
    public void WhenTheSourceIsWrittenWithDifferentSeparators_ThenTheIdStillMatches()
    {
        Assert.Equal(
            AggregateBuilder.An().For("1/2/3").Limited(100).Build().Id,
            AggregateBuilder.An().For("1 2 3").Limited(100).Build().Id);
    }

    // Linked licences are ordered before being appended, so the order they were found in does not
    // produce a different aggregate.
    [Fact]
    public void WhenLinkedLicencesArriveInADifferentOrder_ThenTheIdStillMatches()
    {
        Assert.Equal(
            AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6", "7/8/9").Build().Id,
            AggregateBuilder.An().For("1/2/3").LinkedTo("7/8/9", "4/5/6").Build().Id);
    }

    [Fact]
    public void WhenTheCombinedLimitDiffers_ThenTheIdDiffers()
    {
        Assert.NotEqual(
            AggregateBuilder.An().For("1/2/3").Limited(100).Build().Id,
            AggregateBuilder.An().For("1/2/3").Limited(200).Build().Id);
    }

    // WRADI-547. GetCombinedLimitValue accumulates in a double and returns an unchecked (int)
    // cast, which saturates at int.MaxValue. Every aggregate above that total reports the same
    // value, so distinct aggregates collapse onto one Id - and Id is what the dedup and the
    // duplicate assertion work from.
    [Fact]
    public void WhenTheCombinedLimitExceedsIntMax_ThenDistinctAggregatesShareAnId()
    {
        var fiveBillion = AggregateBuilder.An().For("1/2/3").Limited(5_000_000_000).Build();
        var nineBillion = AggregateBuilder.An().For("1/2/3").Limited(9_000_000_000).Build();

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
            AggregateBuilder.An().For("1/2/3").Limited(5_000_000_000).Build(),
            AggregateBuilder.An().For("4/5/6").Limited(9_000_000_000).Build()
        };

        Assert.Throws<OverflowException>(() => aggregates.Sum(a => a.GetCombinedLimitValue()));
    }

    [Fact]
    public void WhenLimitValuesAreMissing_ThenTheyAreSkippedRatherThanCountedAsZero()
    {
        var withNull = AggregateBuilder.An().For("1/2/3").Limited(100, null).Build();

        Assert.Equal(100, withNull.GetCombinedLimitValue());
    }
}
