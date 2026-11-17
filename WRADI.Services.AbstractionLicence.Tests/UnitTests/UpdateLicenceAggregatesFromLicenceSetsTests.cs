using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Pushing a licence set's aggregates back down onto the licences the set names.
/// </summary>
public class UpdateLicenceAggregatesFromLicenceSetsTests
{
    [Fact]
    public void WhenTheLicenceAlreadyLinksToEverythingTheSetAggregateNames_ThenItGainsThatAggregate()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var ownAggregate = AggregateBuilder.An().For("1/2/3").Limited(50).Build();

        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6"))
            .Holding(ownAggregate)
            .Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSetBuilder.A().Containing(licence).WithAggregateSets(setAggregate).Build()]);

        Assert.Equal(2, licence.AbstractionLimits.Aggregates!.Length);
        Assert.Contains(setAggregate, licence.AbstractionLimits.Aggregates!);
    }

    [Fact]
    public void WhenTheLicenceAlreadyHoldsAnEquivalentAggregate_ThenTheSetAggregateIsNotAdded()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var ownAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6"))
            .Holding(ownAggregate)
            .Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSetBuilder.A().Containing(licence).WithAggregateSets(setAggregate).Build()]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheLicencesAggregateIsAVariationOfTheSetsAndLinksAreMissing_ThenItIsReplaced()
    {
        var ownAggregate = AggregateBuilder.An().For("1/2/3").Build();

        var setAggregate = AggregateBuilder.An()
            .For("1/2/3")
            .LinkedTo("4/5/6")
            .Varying(AggregateVariation.FromAggregate(ownAggregate, "known variation"))
            .Build();

        var licence = LicenceBuilder.A().Numbered("1/2/3").Holding(ownAggregate).Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSetBuilder.A().Containing(licence).WithAggregateSets(setAggregate).Build()]);

        Assert.Same(setAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenALinkIsMissing_ThenItIsSynthesisedFromAnotherLicenceInTheSet()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .Holding(AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        var donor = LicenceBuilder.A()
            .Numbered("9/9/9")
            .LinkedTo(LicenceBuilder.LinkTo(
                "4/5/6",
                [AggregateBuilder.SeenIn("Schedule", linkReason: "named in the aggregate")]))
            .Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
        [
            LicenceSetBuilder.A().Containing(licence, donor).WithAggregateSets(setAggregate).Build()
        ]);

        var synthesised = Assert.Single(licence.LinkedLicences);
        Assert.Equal("4/5/6", synthesised.LicenceNumber);
        Assert.True(synthesised.IsBecauseOfAggregate);
        Assert.Equal("Schedule", synthesised.ContainedIn![0].SectionName);
        Assert.Equal("named in the aggregate", synthesised.ContainedIn![0].LinkReason);
        Assert.Equal(InformationDirection.InSameGroup, synthesised.ContainedIn![0].Direction);
        Assert.Equal(InformationSource.OtherDocument, synthesised.ContainedIn![0].Source);
    }

    // The licence's own aggregate covers fewer licences for the same total, so the set aggregate
    // counts as its superset and absorbs it rather than sitting beside it.
    [Fact]
    public void WhenTheSetAggregateCoversTheLicencesOwn_ThenTheOwnBecomesAVariationOfIt()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var ownAggregate = AggregateBuilder.An().For("1/2/3").Build();

        var licence = LicenceBuilder.A().Numbered("1/2/3").Holding(ownAggregate).Build();

        var donor = LicenceBuilder.A()
            .Numbered("9/9/9")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", [AggregateBuilder.SeenIn()]))
            .Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
        [
            LicenceSetBuilder.A().Containing(licence, donor).WithAggregateSets(setAggregate).Build()
        ]);

        Assert.Same(setAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
        Assert.Equal(ownAggregate.Id, Assert.Single(setAggregate.Variations!).Id);
    }

    // The donor's first ContainedIn entry is read without checking there is one.
    [Fact]
    public void WhenTheDonorLinkHasNoContainedInEntries_ThenItThrows()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .Holding(AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        var donor = LicenceBuilder.A()
            .Numbered("9/9/9")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6", []))
            .Build();

        Assert.Throws<IndexOutOfRangeException>(() =>
            AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [
                LicenceSetBuilder.A()
                    .Containing(licence, donor)
                    .WithAggregateSets(setAggregate)
                    .Build()
            ]));
    }

    // Licences are matched on the number exactly as written, unlike Aggregate.Id, which strips
    // separators first - so the same licence written two ways is not found.
    [Fact]
    public void WhenTheLicenceNumberIsWrittenWithDifferentSeparators_ThenTheLicenceIsNotFound()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var ownAggregate = AggregateBuilder.An().For("1/2/3").Limited(50).Build();

        var licence = LicenceBuilder.A()
            .Numbered("1 2 3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6"))
            .Holding(ownAggregate)
            .Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSetBuilder.A().Containing(licence).WithAggregateSets(setAggregate).Build()]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheSetHasNoAggregateSets_ThenNothingChanges()
    {
        var ownAggregate = AggregateBuilder.An().For("1/2/3").Build();
        var licence = LicenceBuilder.A().Numbered("1/2/3").Holding(ownAggregate).Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSetBuilder.A().Containing(licence).Build()]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheSetAggregateHasNoLinkedLicences_ThenItIsSkipped()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").WithNullLinkedLicences().Build();
        var ownAggregate = AggregateBuilder.An().For("1/2/3").Build();
        var licence = LicenceBuilder.A().Numbered("1/2/3").Holding(ownAggregate).Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSetBuilder.A().Containing(licence).WithAggregateSets(setAggregate).Build()]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheNamedLicenceHasNoAggregates_ThenItIsLeftAlone()
    {
        var setAggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        var licence = LicenceBuilder.A()
            .Numbered("1/2/3")
            .LinkedTo(LicenceBuilder.LinkTo("4/5/6"))
            .Build();

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSetBuilder.A().Containing(licence).WithAggregateSets(setAggregate).Build()]);

        Assert.Null(licence.AbstractionLimits.Aggregates);
    }
}
