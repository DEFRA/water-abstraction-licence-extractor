using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Aggregate.Id is what DeDuplicateAggregates dedupes on and AssertNoDuplicateAggregates groups
/// by, so two aggregates sharing an Id are treated as one.
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
    
    [Fact]
    public void WhenAnAggregateIsCloned_ThenTheDescriptionIsKept()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").Limited(100).Build();
        aggregate.Description = "Some notes";

        Assert.Equal("Some notes", aggregate.Clone().Description);
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

    // WRADI-547. A litre total above int.MaxValue is ordinary for one licence, and the value used
    // to be cast to int, so every aggregate past that point reported int.MaxValue and collapsed
    // onto one Id - which is what the dedup and the duplicate assertion work from.
    [Fact]
    public void WhenTheCombinedLimitExceedsIntMax_ThenAggregatesKeepDistinctIds()
    {
        var fiveBillion = AggregateBuilder.An().For("1/2/3").Limited(5_000_000_000).Build();
        var nineBillion = AggregateBuilder.An().For("1/2/3").Limited(9_000_000_000).Build();

        Assert.Equal(5_000_000_000, fiveBillion.GetCombinedLimitValue());
        Assert.Equal(9_000_000_000, nineBillion.GetCombinedLimitValue());
        Assert.NotEqual(fiveBillion.Id, nineBillion.Id);
    }

    [Fact]
    public void WhenCombinedLimitsAreSummedAcrossAggregates_ThenTheTotalIsExact()
    {
        var aggregates = new[]
        {
            AggregateBuilder.An().For("1/2/3").Limited(5_000_000_000).Build(),
            AggregateBuilder.An().For("4/5/6").Limited(9_000_000_000).Build()
        };

        Assert.Equal(14_000_000_000, aggregates.Sum(a => a.GetCombinedLimitValue()));
    }

    [Fact]
    public void WhenLimitValuesAreMissing_ThenTheyAreSkippedRatherThanCountedAsZero()
    {
        var withNull = AggregateBuilder.An().For("1/2/3").Limited(100, null).Build();

        Assert.Equal(100, withNull.GetCombinedLimitValue());
    }
}
