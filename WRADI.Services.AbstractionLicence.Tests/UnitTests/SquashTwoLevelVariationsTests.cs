using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Flattening a variation's own variations up into its parent's list. These state current
/// behaviour, not desired behaviour.
/// </summary>
public class SquashTwoLevelVariationsTests
{
    private static AggregateVariation Variation(string sourceLicence, string? difference = null) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = [],
        Limits = [new AbstractionLimit { Value = 100 }],
        Difference = difference
    };

    private static Aggregate Aggregate(params AggregateVariation[] variations) => new()
    {
        SourceLicenceNumber = "1/1/1",
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = [],
        Limits = [new AbstractionLimit { Value = 100 }],
        Variations = variations
    };

    private static Licence Licence(ScrapeStatus status, params Aggregate[] aggregates) => new()
    {
        Status = status,
        AbstractionLimits = new AbstractionLimits { Aggregates = aggregates }
    };

    [Fact]
    public void WhenNoVariationIsNested_ThenTheListComesBackUnchanged()
    {
        var squashed = AggregateVariationHelper.SquashTwoLevelVariations(
            [Variation("1/2/3"), Variation("4/5/6")]);

        Assert.Equal(2, squashed.Count);
    }

    [Fact]
    public void WhenAVariationIsNested_ThenItIsPromotedAndItsParentIsEmptied()
    {
        var parent = Variation("1/2/3");
        parent.Variations = [Variation("4/5/6")];

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations([parent]);

        Assert.Equal(2, squashed.Count);
        Assert.Null(parent.Variations);
    }

    [Fact]
    public void WhenTheNestedVariationIsAlreadyInTheList_ThenItIsNotPromotedAgain()
    {
        var parent = Variation("1/2/3");
        parent.Variations = [Variation("4/5/6")];

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations(
            [parent, Variation("4/5/6")]);

        Assert.Equal(2, squashed.Count);
    }

    // Containment is tested against the list that came in, not the list being built, so a child
    // nested under two different parents is promoted once for each of them.
    [Fact]
    public void WhenTwoParentsNestTheSameChild_ThenItIsPromotedTwice()
    {
        var firstParent = Variation("1/2/3");
        firstParent.Variations = [Variation("7/8/9")];

        var secondParent = Variation("4/5/6");
        secondParent.Variations = [Variation("7/8/9")];

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations(
            [firstParent, secondParent]);

        Assert.Equal(2, squashed.Count(v => v.SourceLicenceNumber == "7/8/9"));
    }

    // Only the top level is flattened. A promoted child keeps its own variations, which is the
    // shape AssertNoMultipleLevelVariations throws on.
    [Fact]
    public void WhenVariationsAreThreeDeep_ThenThePromotedChildKeepsItsOwn()
    {
        var grandchild = Variation("7/8/9");

        var child = Variation("4/5/6");
        child.Variations = [grandchild];

        var parent = Variation("1/2/3");
        parent.Variations = [child];

        var squashed = AggregateVariationHelper.SquashTwoLevelVariations([parent]);

        Assert.Equal(2, squashed.Count);
        Assert.Same(grandchild, Assert.Single(child.Variations!));
    }

    [Fact]
    public void WhenALicencesAggregateHasNestedVariations_ThenTheyArePromoted()
    {
        var parent = Variation("1/2/3");
        parent.Variations = [Variation("4/5/6")];

        var aggregate = Aggregate(parent);

        AggregateVariationHelper.SquashTwoLevelVariations([Licence(ScrapeStatus.Ok, aggregate)]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }

    // Unlike the de-duplication passes, this one has no status filter.
    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenItsVariationsAreStillSquashed(ScrapeStatus status)
    {
        var parent = Variation("1/2/3");
        parent.Variations = [Variation("4/5/6")];

        var aggregate = Aggregate(parent);

        AggregateVariationHelper.SquashTwoLevelVariations([Licence(status, aggregate)]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }

    [Fact]
    public void WhenALicenceHasNoAggregates_ThenSquashingDoesNothing()
    {
        var licence = new Licence
        {
            Status = ScrapeStatus.Ok,
            AbstractionLimits = new AbstractionLimits { Aggregates = null }
        };

        AggregateVariationHelper.SquashTwoLevelVariations([licence]);

        Assert.Null(licence.AbstractionLimits.Aggregates);
    }

    [Fact]
    public void WhenALicenceSetHasAggregateSets_ThenBothItsLicencesAndItsSetsAreSquashed()
    {
        var licenceParent = Variation("1/2/3");
        licenceParent.Variations = [Variation("4/5/6")];
        var licenceAggregate = Aggregate(licenceParent);

        var setParent = Variation("2/3/4");
        setParent.Variations = [Variation("5/6/7")];
        var setAggregate = Aggregate(setParent);

        var licenceSet = new LicenceSet
        {
            Licences = [Licence(ScrapeStatus.Ok, licenceAggregate)],
            AggregateSets = [setAggregate]
        };

        AggregateVariationHelper.SquashTwoLevelVariations([licenceSet]);

        Assert.Equal(2, licenceAggregate.Variations!.Length);
        Assert.Equal(2, setAggregate.Variations!.Length);
    }

    [Fact]
    public void WhenALicenceSetHasNoAggregateSets_ThenItsLicencesAreStillSquashed()
    {
        var parent = Variation("1/2/3");
        parent.Variations = [Variation("4/5/6")];
        var aggregate = Aggregate(parent);

        var licenceSet = new LicenceSet
        {
            Licences = [Licence(ScrapeStatus.Ok, aggregate)],
            AggregateSets = null
        };

        AggregateVariationHelper.SquashTwoLevelVariations([licenceSet]);

        Assert.Equal(2, aggregate.Variations!.Length);
    }
}
