using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// The three places a duplicate is collapsed rather than asserted on. These state current
/// behaviour, not desired behaviour.
/// </summary>
public class AggregateDeDuplicationTests
{
    [Fact]
    public void WhenTwoAggregatesShareAnId_ThenOnlyOneSurvives()
    {
        var licence = LicenceBuilder.A()
            .Holding(
                AggregateBuilder.An().For("1/2/3").Build(),
                AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        AggregateHelper.DeDuplicateAggregates([licence]);

        Assert.Single(licence.AbstractionLimits.Aggregates!);
    }

    [Fact]
    public void WhenDuplicatesAreCollapsed_ThenTheirContainedInEntriesAreMerged()
    {
        var licence = LicenceBuilder.A()
            .Holding(
                AggregateBuilder.An().For("1/2/3")
                    .SightedIn(AggregateBuilder.SeenIn("Schedule", 1)).Build(),
                AggregateBuilder.An().For("1/2/3")
                    .SightedIn(AggregateBuilder.SeenIn("Conditions", 2)).Build())
            .Build();

        AggregateHelper.DeDuplicateAggregates([licence]);

        var survivor = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Equal(2, survivor.ContainedIn!.Length);
    }

    // The merge key is Source, SectionName, LineNumber and DocumentIdentifier only, so two
    // sightings that differ in direction, page or link reason collapse to one.
    [Fact]
    public void WhenMergedEntriesDifferOnlyOutsideTheMergeKey_ThenOneIsDiscarded()
    {
        var licence = LicenceBuilder.A()
            .Holding(
                AggregateBuilder.An().For("1/2/3")
                    .SightedIn(AggregateBuilder.SeenIn("Schedule", 1, InformationDirection.Incoming))
                    .Build(),
                AggregateBuilder.An().For("1/2/3")
                    .SightedIn(AggregateBuilder.SeenIn("Schedule", 1, InformationDirection.Outgoing))
                    .Build())
            .Build();

        AggregateHelper.DeDuplicateAggregates([licence]);

        var survivor = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Single(survivor.ContainedIn!);
    }

    // The merge runs only when a duplicate aggregate was found, so an aggregate that is unique
    // keeps repeated sightings it would have lost had it been duplicated.
    [Fact]
    public void WhenNoAggregateIsDuplicated_ThenRepeatedContainedInEntriesAreLeftAlone()
    {
        var licence = LicenceBuilder.A()
            .Holding(
                AggregateBuilder.An().For("1/2/3")
                    .SightedIn(
                        AggregateBuilder.SeenIn("Schedule", 1),
                        AggregateBuilder.SeenIn("Schedule", 1))
                    .Build())
            .Build();

        AggregateHelper.DeDuplicateAggregates([licence]);

        var only = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Equal(2, only.ContainedIn!.Length);
    }

    [Fact]
    public void WhenCollapsedDuplicatesHadNoContainedIn_ThenTheSurvivorGetsAnEmptyArray()
    {
        var licence = LicenceBuilder.A()
            .Holding(
                AggregateBuilder.An().For("1/2/3").Build(),
                AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        AggregateHelper.DeDuplicateAggregates([licence]);

        var survivor = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Empty(survivor.ContainedIn!);
    }

    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenDuplicateAggregatesSurvive(ScrapeStatus status)
    {
        var licence = LicenceBuilder.A()
            .ThatDidNotExtractCleanly(status)
            .Holding(
                AggregateBuilder.An().For("1/2/3").Build(),
                AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        AggregateHelper.DeDuplicateAggregates([licence]);

        Assert.Equal(2, licence.AbstractionLimits.Aggregates!.Length);
    }

    [Fact]
    public void WhenALicenceHasNoAggregates_ThenDeDuplicatingDoesNothing()
    {
        var licence = LicenceBuilder.A().Build();

        AggregateHelper.DeDuplicateAggregates([licence]);

        Assert.Null(licence.AbstractionLimits.Aggregates);
    }

    [Fact]
    public void WhenVariationsAreNull_ThenDeDupingThemReturnsNull()
    {
        Assert.Null(AggregateVariationHelper.DeDupeVariations(null));
    }

    [Fact]
    public void WhenTwoVariationsShareAnId_ThenTheFirstIsKept()
    {
        var deduped = AggregateVariationHelper.DeDupeVariations(
        [
            AggregateBuilder.An().For("9/9/9").BuildVariation("first"),
            AggregateBuilder.An().For("9/9/9").BuildVariation("second")
        ]);

        var survivor = Assert.Single(deduped!);
        Assert.Equal("first", survivor.Difference);
    }

    [Fact]
    public void WhenAnAggregateHasTwoVariationsWithTheSameId_ThenRemovingDuplicatesKeepsTheFirst()
    {
        var aggregate = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(
                AggregateBuilder.An().For("9/9/9").BuildVariation("first"),
                AggregateBuilder.An().For("9/9/9").BuildVariation("second"))
            .Build();

        AggregateVariationHelper.RemoveDuplicateVariations(
            [LicenceBuilder.A().Holding(aggregate).Build()]);

        var survivor = Assert.Single(aggregate.Variations!);
        Assert.Equal("first", survivor.Difference);
    }

    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenDuplicateVariationsSurvive(ScrapeStatus status)
    {
        var aggregate = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(
                AggregateBuilder.An().For("9/9/9").BuildVariation("first"),
                AggregateBuilder.An().For("9/9/9").BuildVariation("second"))
            .Build();

        AggregateVariationHelper.RemoveDuplicateVariations(
        [
            LicenceBuilder.A().ThatDidNotExtractCleanly(status).Holding(aggregate).Build()
        ]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }
}
