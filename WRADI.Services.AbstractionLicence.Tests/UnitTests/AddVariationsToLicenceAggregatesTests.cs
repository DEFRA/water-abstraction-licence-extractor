using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Cross-licence variation building: one licence's aggregate picking up another licence's
/// equivalent aggregate as a variation.
/// </summary>
public class AddVariationsToLicenceAggregatesTests
{
    [Fact]
    public void WhenAnotherLicenceHoldsTheSameLinkTheOtherWayRound_ThenItBecomesAVariation()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var mirrored = AggregateBuilder.An().For("4/5/6").LinkedTo("1/2/3").Build();

        AggregateVariationHelper.AddVariationsToLicenceAggregates(
        [
            LicenceBuilder.A().Holding(aggregate).Build(),
            LicenceBuilder.A().Holding(mirrored).Build()
        ]);

        var variation = Assert.Single(aggregate.Variations!);
        Assert.Equal(mirrored.Id, variation.Id);
        Assert.Equal(AggregateVariation.DifferentOrder, variation.Difference);
    }

    [Fact]
    public void WhenTheOtherAggregateIsNotEquivalent_ThenNoVariationIsAdded()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var unrelated = AggregateBuilder.An().For("7/8/9").LinkedTo("0/0/0").Build();

        AggregateVariationHelper.AddVariationsToLicenceAggregates(
        [
            LicenceBuilder.A().Holding(aggregate).Build(),
            LicenceBuilder.A().Holding(unrelated).Build()
        ]);

        Assert.Null(aggregate.Variations);
    }

    // A variation carrying the host aggregate's own Id is dropped, so two licences holding the
    // identical aggregate produce no variation at all.
    [Fact]
    public void WhenTheTwoAggregatesAreIdentical_ThenNoVariationSurvives()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var identical = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        AggregateVariationHelper.AddVariationsToLicenceAggregates(
        [
            LicenceBuilder.A().Holding(aggregate).Build(),
            LicenceBuilder.A().Holding(identical).Build()
        ]);

        Assert.Null(aggregate.Variations);
    }

    [Fact]
    public void WhenTheVariationIsAlreadyHeld_ThenItIsNotAddedAgain()
    {
        var mirrored = AggregateBuilder.An().For("4/5/6").LinkedTo("1/2/3").Build();

        var aggregate = AggregateBuilder.An()
            .For("1/2/3")
            .LinkedTo("4/5/6")
            .Varying(AggregateVariation.FromAggregate(mirrored, "already here"))
            .Build();

        AggregateVariationHelper.AddVariationsToLicenceAggregates(
        [
            LicenceBuilder.A().Holding(aggregate).Build(),
            LicenceBuilder.A().Holding(mirrored).Build()
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
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var mirrored = AggregateBuilder.An().For("4/5/6").LinkedTo("1/2/3").Build();

        AggregateVariationHelper.AddVariationsToLicenceAggregates(
        [
            LicenceBuilder.A().ThatDidNotExtractCleanly(status).Holding(aggregate).Build(),
            LicenceBuilder.A().ThatDidNotExtractCleanly(status).Holding(mirrored).Build()
        ]);

        Assert.Single(aggregate.Variations!);
    }

    [Fact]
    public void WhenTheOtherLicenceHasNoAggregates_ThenNothingIsAdded()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        AggregateVariationHelper.AddVariationsToLicenceAggregates(
        [
            LicenceBuilder.A().Holding(aggregate).Build(),
            LicenceBuilder.A().Build()
        ]);

        Assert.Null(aggregate.Variations);
    }
}
