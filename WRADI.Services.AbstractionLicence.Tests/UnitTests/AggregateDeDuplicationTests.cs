using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// The three places a duplicate is collapsed rather than asserted on. These state current
/// behaviour, not desired behaviour.
/// </summary>
public class AggregateDeDuplicationTests
{
    private static Aggregate Aggregate(string sourceLicence, params ContainedInInformation[] containedIn) =>
        new()
        {
            SourceLicenceNumber = sourceLicence,
            PrimaryType = PrimaryType.LicenceToLicence,
            SubType = SubType.PointToPoint,
            LinkedLicences = [],
            Limits = [new AbstractionLimit { Value = 100 }],
            ContainedIn = containedIn.Length == 0 ? null : containedIn
        };

    private static AggregateVariation Variation(string sourceLicence, string difference) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = [],
        Limits = [new AbstractionLimit { Value = 100 }],
        Difference = difference
    };

    private static ContainedInInformation ContainedIn(
        string sectionName,
        int lineNumber,
        InformationDirection? direction = null) =>
        new()
        {
            Source = InformationSource.Document,
            SectionName = sectionName,
            LineNumber = lineNumber,
            Direction = direction
        };

    private static Licence Licence(ScrapeStatus status, params Aggregate[] aggregates) => new()
    {
        Status = status,
        AbstractionLimits = new AbstractionLimits { Aggregates = aggregates }
    };

    [Fact]
    public void WhenTwoAggregatesShareAnId_ThenOnlyOneSurvives()
    {
        var licence = Licence(ScrapeStatus.Ok, Aggregate("1/2/3"), Aggregate("1/2/3"));

        AggregateHelper.DeDuplicateAggregates([licence]);

        Assert.Single(licence.AbstractionLimits.Aggregates!);
    }

    [Fact]
    public void WhenDuplicatesAreCollapsed_ThenTheirContainedInEntriesAreMerged()
    {
        var licence = Licence(
            ScrapeStatus.Ok,
            Aggregate("1/2/3", ContainedIn("Schedule", 1)),
            Aggregate("1/2/3", ContainedIn("Conditions", 2)));

        AggregateHelper.DeDuplicateAggregates([licence]);

        var survivor = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Equal(2, survivor.ContainedIn!.Length);
    }

    // The merge key is Source, SectionName, LineNumber and DocumentIdentifier only, so two
    // sightings that differ in direction, page or link reason collapse to one.
    [Fact]
    public void WhenMergedEntriesDifferOnlyOutsideTheMergeKey_ThenOneIsDiscarded()
    {
        var licence = Licence(
            ScrapeStatus.Ok,
            Aggregate("1/2/3", ContainedIn("Schedule", 1, InformationDirection.Incoming)),
            Aggregate("1/2/3", ContainedIn("Schedule", 1, InformationDirection.Outgoing)));

        AggregateHelper.DeDuplicateAggregates([licence]);

        var survivor = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Single(survivor.ContainedIn!);
    }

    // The merge runs only when a duplicate aggregate was found, so an aggregate that is unique
    // keeps repeated sightings it would have lost had it been duplicated.
    [Fact]
    public void WhenNoAggregateIsDuplicated_ThenRepeatedContainedInEntriesAreLeftAlone()
    {
        var licence = Licence(
            ScrapeStatus.Ok,
            Aggregate("1/2/3", ContainedIn("Schedule", 1), ContainedIn("Schedule", 1)));

        AggregateHelper.DeDuplicateAggregates([licence]);

        var only = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Equal(2, only.ContainedIn!.Length);
    }

    [Fact]
    public void WhenCollapsedDuplicatesHadNoContainedIn_ThenTheSurvivorGetsAnEmptyArray()
    {
        var licence = Licence(ScrapeStatus.Ok, Aggregate("1/2/3"), Aggregate("1/2/3"));

        AggregateHelper.DeDuplicateAggregates([licence]);

        var survivor = Assert.Single(licence.AbstractionLimits.Aggregates!);
        Assert.Empty(survivor.ContainedIn!);
    }

    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenDuplicateAggregatesSurvive(ScrapeStatus status)
    {
        var licence = Licence(status, Aggregate("1/2/3"), Aggregate("1/2/3"));

        AggregateHelper.DeDuplicateAggregates([licence]);

        Assert.Equal(2, licence.AbstractionLimits.Aggregates!.Length);
    }

    [Fact]
    public void WhenALicenceHasNoAggregates_ThenDeDuplicatingDoesNothing()
    {
        var licence = new Licence
        {
            Status = ScrapeStatus.Ok,
            AbstractionLimits = new AbstractionLimits { Aggregates = null }
        };

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
            [Variation("9/9/9", "first"), Variation("9/9/9", "second")]);

        var survivor = Assert.Single(deduped!);
        Assert.Equal("first", survivor.Difference);
    }

    [Fact]
    public void WhenAnAggregateHasTwoVariationsWithTheSameId_ThenRemovingDuplicatesKeepsTheFirst()
    {
        var aggregate = Aggregate("1/2/3");
        aggregate.Variations = [Variation("9/9/9", "first"), Variation("9/9/9", "second")];

        AggregateVariationHelper.RemoveDuplicateVariations([Licence(ScrapeStatus.Ok, aggregate)]);

        var survivor = Assert.Single(aggregate.Variations!);
        Assert.Equal("first", survivor.Difference);
    }

    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenDuplicateVariationsSurvive(ScrapeStatus status)
    {
        var aggregate = Aggregate("1/2/3");
        aggregate.Variations = [Variation("9/9/9", "first"), Variation("9/9/9", "second")];

        AggregateVariationHelper.RemoveDuplicateVariations([Licence(status, aggregate)]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }
}
