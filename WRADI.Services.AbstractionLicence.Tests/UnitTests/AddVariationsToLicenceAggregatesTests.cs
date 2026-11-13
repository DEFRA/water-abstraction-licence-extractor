using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Cross-licence variation building: one licence's aggregate picking up another licence's
/// equivalent aggregate as a variation. These state current behaviour, not desired behaviour.
/// </summary>
public class AddVariationsToLicenceAggregatesTests
{
    private static Aggregate Aggregate(string sourceLicence, string[] linked, double limit = 100) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = linked,
        Limits = [new AbstractionLimit { Value = limit }]
    };

    private static Licence Licence(ScrapeStatus status, params Aggregate[] aggregates) => new()
    {
        Status = status,
        AbstractionLimits = new AbstractionLimits { Aggregates = aggregates }
    };

    [Fact]
    public void WhenAnotherLicenceHoldsTheSameLinkTheOtherWayRound_ThenItBecomesAVariation()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"]);
        var mirrored = Aggregate("4/5/6", ["1/2/3"]);

        AbstractionLicenceSchemaConverter.AddVariationsToLicenceAggregates(
        [
            Licence(ScrapeStatus.Ok, aggregate),
            Licence(ScrapeStatus.Ok, mirrored)
        ]);

        var variation = Assert.Single(aggregate.Variations!);
        Assert.Equal(mirrored.Id, variation.Id);
        Assert.Equal(AggregateVariation.DifferentOrder, variation.Difference);
    }

    [Fact]
    public void WhenTheOtherAggregateIsNotEquivalent_ThenNoVariationIsAdded()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"]);
        var unrelated = Aggregate("7/8/9", ["0/0/0"]);

        AbstractionLicenceSchemaConverter.AddVariationsToLicenceAggregates(
        [
            Licence(ScrapeStatus.Ok, aggregate),
            Licence(ScrapeStatus.Ok, unrelated)
        ]);

        Assert.Null(aggregate.Variations);
    }

    // A variation carrying the host aggregate's own Id is dropped, so two licences holding the
    // identical aggregate produce no variation at all.
    [Fact]
    public void WhenTheTwoAggregatesAreIdentical_ThenNoVariationSurvives()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"]);
        var identical = Aggregate("1/2/3", ["4/5/6"]);

        AbstractionLicenceSchemaConverter.AddVariationsToLicenceAggregates(
        [
            Licence(ScrapeStatus.Ok, aggregate),
            Licence(ScrapeStatus.Ok, identical)
        ]);

        Assert.Null(aggregate.Variations);
    }

    [Fact]
    public void WhenTheVariationIsAlreadyHeld_ThenItIsNotAddedAgain()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"]);
        var mirrored = Aggregate("4/5/6", ["1/2/3"]);

        aggregate.Variations =
        [
            AggregateVariation.FromAggregate(mirrored, "already here")
        ];

        AbstractionLicenceSchemaConverter.AddVariationsToLicenceAggregates(
        [
            Licence(ScrapeStatus.Ok, aggregate),
            Licence(ScrapeStatus.Ok, mirrored)
        ]);

        var variation = Assert.Single(aggregate.Variations!);
        Assert.Equal("already here", variation.Difference);
    }

    // No status filter here, unlike the de-duplication passes.
    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenNeitherLicenceExtractedCleanly_ThenTheVariationIsStillAdded(ScrapeStatus status)
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"]);
        var mirrored = Aggregate("4/5/6", ["1/2/3"]);

        AbstractionLicenceSchemaConverter.AddVariationsToLicenceAggregates(
        [
            Licence(status, aggregate),
            Licence(status, mirrored)
        ]);

        Assert.Single(aggregate.Variations!);
    }

    [Fact]
    public void WhenTheOtherLicenceHasNoAggregates_ThenNothingIsAdded()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"]);

        AbstractionLicenceSchemaConverter.AddVariationsToLicenceAggregates(
        [
            Licence(ScrapeStatus.Ok, aggregate),
            new Licence
            {
                Status = ScrapeStatus.Ok,
                AbstractionLimits = new AbstractionLimits { Aggregates = null }
            }
        ]);

        Assert.Null(aggregate.Variations);
    }
}
