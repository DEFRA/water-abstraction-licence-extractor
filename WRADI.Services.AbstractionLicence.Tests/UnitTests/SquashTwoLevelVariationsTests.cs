using WALE.ProcessFile.Core.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Flattening a variation's own variations up into its parent's list. These state current
/// behaviour, not desired behaviour.
/// </summary>
public class SquashTwoLevelVariationsTests
{
    [Fact]
    public void WhenNoVariationIsNested_ThenTheListComesBackUnchanged()
    {
        var squashed = AggregateVariationHelper.SquashTwoLevelVariations(
        [
            AggregateBuilder.An().For("1/2/3").BuildVariation(),
            AggregateBuilder.An().For("4/5/6").BuildVariation()
        ]);

        Assert.Equal(2, squashed.Count);
    }

    [Fact]
    public void WhenAVariationIsNested_ThenItIsPromotedAndItsParentIsEmptied()
    {
        var parent = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(AggregateBuilder.An().For("4/5/6").BuildVariation())
            .BuildVariation();

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations([parent]);

        Assert.Equal(2, squashed.Count);
        Assert.Null(parent.Variations);
    }

    [Fact]
    public void WhenTheNestedVariationIsAlreadyInTheList_ThenItIsNotPromotedAgain()
    {
        var parent = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(AggregateBuilder.An().For("4/5/6").BuildVariation())
            .BuildVariation();

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations(
            [parent, AggregateBuilder.An().For("4/5/6").BuildVariation()]);

        Assert.Equal(2, squashed.Count);
    }

    // Containment is tested against the list that came in, not the list being built, so a child
    // nested under two different parents is promoted once for each of them.
    [Fact]
    public void WhenTwoParentsNestTheSameChild_ThenItIsPromotedTwice()
    {
        var firstParent = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(AggregateBuilder.An().For("7/8/9").BuildVariation())
            .BuildVariation();

        var secondParent = AggregateBuilder.An()
            .For("4/5/6")
            .Varying(AggregateBuilder.An().For("7/8/9").BuildVariation())
            .BuildVariation();

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations(
            [firstParent, secondParent]);

        Assert.Equal(2, squashed.Count(v => v.SourceLicenceNumber == "7/8/9"));
    }

    // Only the top level is flattened. A promoted child keeps its own variations, which is the
    // shape AssertNoMultipleLevelVariations throws on.
    [Fact]
    public void WhenVariationsAreThreeDeep_ThenThePromotedChildKeepsItsOwn()
    {
        var grandchild = AggregateBuilder.An().For("7/8/9").BuildVariation();

        var child = AggregateBuilder.An().For("4/5/6").Varying(grandchild).BuildVariation();
        var parent = AggregateBuilder.An().For("1/2/3").Varying(child).BuildVariation();

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations([parent]);

        Assert.Equal(2, squashed.Count);
        Assert.Same(grandchild, Assert.Single(child.Variations!));
    }

    [Fact]
    public void WhenALicencesAggregateHasNestedVariations_ThenTheyArePromoted()
    {
        var parent = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(AggregateBuilder.An().For("4/5/6").BuildVariation())
            .BuildVariation();

        var aggregate = AggregateBuilder.An().For("1/1/1").Varying(parent).Build();

        AggregateVariationHelper.SquashTwoLevelVariations(
            [LicenceBuilder.A().Holding(aggregate).Build()]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }

    // Unlike the de-duplication passes, this one has no status filter.
    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenItsVariationsAreStillSquashed(ScrapeStatus status)
    {
        var parent = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(AggregateBuilder.An().For("4/5/6").BuildVariation())
            .BuildVariation();

        var aggregate = AggregateBuilder.An().For("1/1/1").Varying(parent).Build();

        AggregateVariationHelper.SquashTwoLevelVariations(
        [
            LicenceBuilder.A().ThatDidNotExtractCleanly(status).Holding(aggregate).Build()
        ]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }

    [Fact]
    public void WhenALicenceHasNoAggregates_ThenSquashingDoesNothing()
    {
        var licence = LicenceBuilder.A().Build();

        AggregateVariationHelper.SquashTwoLevelVariations([licence]);

        Assert.Null(licence.AbstractionLimits.Aggregates);
    }

    [Fact]
    public void WhenALicenceSetHasAggregateSets_ThenBothItsLicencesAndItsSetsAreSquashed()
    {
        var licenceParent = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(AggregateBuilder.An().For("4/5/6").BuildVariation())
            .BuildVariation();

        var licenceAggregate = AggregateBuilder.An().For("1/1/1").Varying(licenceParent).Build();

        var setParent = AggregateBuilder.An()
            .For("2/3/4")
            .Varying(AggregateBuilder.An().For("5/6/7").BuildVariation())
            .BuildVariation();

        var setAggregate = AggregateBuilder.An().For("1/1/1").Varying(setParent).Build();

        AggregateVariationHelper.SquashTwoLevelVariations(
        [
            LicenceSetBuilder.A()
                .Containing(LicenceBuilder.A().Holding(licenceAggregate).Build())
                .WithAggregateSets(setAggregate)
                .Build()
        ]);

        Assert.Equal(2, licenceAggregate.Variations!.Length);
        Assert.Equal(2, setAggregate.Variations!.Length);
    }

    [Fact]
    public void WhenALicenceSetHasNoAggregateSets_ThenItsLicencesAreStillSquashed()
    {
        var parent = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(AggregateBuilder.An().For("4/5/6").BuildVariation())
            .BuildVariation();

        var aggregate = AggregateBuilder.An().For("1/1/1").Varying(parent).Build();

        AggregateVariationHelper.SquashTwoLevelVariations(
        [
            LicenceSetBuilder.A()
                .Containing(LicenceBuilder.A().Holding(aggregate).Build())
                .Build()
        ]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }
}
