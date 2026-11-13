using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Pushing a licence set's aggregates back down onto the licences the set names. These state
/// current behaviour, not desired behaviour.
/// </summary>
public class UpdateLicenceAggregatesFromLicenceSetsTests
{
    private static Aggregate Aggregate(string sourceLicence, string[] linked, double limit = 100) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = linked,
        Limits = [new AbstractionLimit { Value = limit }]
    };

    private static LinkedLicence LinkedTo(string licenceNumber, ContainedInInformation[]? containedIn = null) => new()
    {
        LicenceNumber = licenceNumber,
        ContainedIn = containedIn
    };

    private static Licence Licence(
        string licenceNumber,
        LinkedLicence[] linkedLicences,
        Aggregate[]? aggregates) =>
        new()
        {
            Status = ScrapeStatus.Ok,
            LicenceNumber = new ValueWithConfidence<string>(licenceNumber, null, null),
            LinkedLicences = linkedLicences,
            AbstractionLimits = new AbstractionLimits { Aggregates = aggregates }
        };

    private static LicenceSet LicenceSet(Licence[] licences, Aggregate[]? aggregateSets) => new()
    {
        LicenceSetType = LicenceSetType.LicencesGroupedByAbstractionLimits,
        Licences = licences,
        AggregateSets = aggregateSets
    };

    [Fact]
    public void WhenTheLicenceAlreadyLinksToEverythingTheSetAggregateNames_ThenItGainsThatAggregate()
    {
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var ownAggregate = Aggregate("1/2/3", [], 50);
        var licence = Licence("1/2/3", [LinkedTo("4/5/6")], [ownAggregate]);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence], [setAggregate])]);

        Assert.Equal(2, licence.AbstractionLimits.Aggregates!.Length);
        Assert.Contains(setAggregate, licence.AbstractionLimits.Aggregates!);
    }

    [Fact]
    public void WhenTheLicenceAlreadyHoldsAnEquivalentAggregate_ThenTheSetAggregateIsNotAdded()
    {
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var ownAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var licence = Licence("1/2/3", [LinkedTo("4/5/6")], [ownAggregate]);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence], [setAggregate])]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheLicencesAggregateIsAVariationOfTheSetsAndLinksAreMissing_ThenItIsReplaced()
    {
        var ownAggregate = Aggregate("1/2/3", []);
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        setAggregate.Variations = [AggregateVariation.FromAggregate(ownAggregate, "known variation")];

        var licence = Licence("1/2/3", [], [ownAggregate]);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence], [setAggregate])]);

        Assert.Same(setAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenALinkIsMissing_ThenItIsSynthesisedFromAnotherLicenceInTheSet()
    {
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var licence = Licence("1/2/3", [], [Aggregate("1/2/3", [])]);

        var donor = Licence(
            "9/9/9",
            [
                LinkedTo("4/5/6", [new ContainedInInformation
                {
                    Source = InformationSource.Document,
                    SectionName = "Schedule",
                    LinkReason = "named in the aggregate"
                }])
            ],
            null);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence, donor], [setAggregate])]);

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
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var ownAggregate = Aggregate("1/2/3", []);
        var licence = Licence("1/2/3", [], [ownAggregate]);

        var donor = Licence(
            "9/9/9",
            [LinkedTo("4/5/6", [new ContainedInInformation { Source = InformationSource.Document }])],
            null);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence, donor], [setAggregate])]);

        Assert.Same(setAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
        Assert.Equal(ownAggregate.Id, Assert.Single(setAggregate.Variations!).Id);
    }

    // The donor's first ContainedIn entry is read without checking there is one.
    [Fact]
    public void WhenTheDonorLinkHasNoContainedInEntries_ThenItThrows()
    {
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var licence = Licence("1/2/3", [], [Aggregate("1/2/3", [])]);
        var donor = Licence("9/9/9", [LinkedTo("4/5/6", [])], null);

        Assert.Throws<IndexOutOfRangeException>(() =>
            AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
                [LicenceSet([licence, donor], [setAggregate])]));
    }

    // Licences are matched on the number exactly as written, unlike Aggregate.Id, which strips
    // separators first - so the same licence written two ways is not found.
    [Fact]
    public void WhenTheLicenceNumberIsWrittenWithDifferentSeparators_ThenTheLicenceIsNotFound()
    {
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var ownAggregate = Aggregate("1/2/3", [], 50);
        var licence = Licence("1 2 3", [LinkedTo("4/5/6")], [ownAggregate]);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence], [setAggregate])]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheSetHasNoAggregateSets_ThenNothingChanges()
    {
        var ownAggregate = Aggregate("1/2/3", []);
        var licence = Licence("1/2/3", [], [ownAggregate]);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence], null)]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheSetAggregateHasNoLinkedLicences_ThenItIsSkipped()
    {
        var setAggregate = Aggregate("1/2/3", []);
        setAggregate.LinkedLicences = null;

        var ownAggregate = Aggregate("1/2/3", []);
        var licence = Licence("1/2/3", [], [ownAggregate]);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence], [setAggregate])]);

        Assert.Same(ownAggregate, Assert.Single(licence.AbstractionLimits.Aggregates!));
    }

    [Fact]
    public void WhenTheNamedLicenceHasNoAggregates_ThenItIsLeftAlone()
    {
        var setAggregate = Aggregate("1/2/3", ["4/5/6"]);
        var licence = Licence("1/2/3", [LinkedTo("4/5/6")], null);

        AggregateHelper.UpdateLicenceAggregatesFromLicenceSets(
            [LicenceSet([licence], [setAggregate])]);

        Assert.Null(licence.AbstractionLimits.Aggregates);
    }
}
