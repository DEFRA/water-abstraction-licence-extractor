using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Which of a set of candidates counts as covering a given aggregate or licence set. These state
/// current behaviour, not desired behaviour.
/// </summary>
public class GetSupersetsTests
{
    private static Aggregate Aggregate(string sourceLicence, string[] linked, double limit) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = linked,
        Limits = [new AbstractionLimit { Value = limit }]
    };

    private static Licence Licence(string licenceNumber) => new()
    {
        Status = ScrapeStatus.Ok,
        LicenceNumber = new ValueWithConfidence<string>(licenceNumber, null, null),
        AbstractionLimits = new AbstractionLimits()
    };

    private static LicenceSet LicenceSet(
        LicenceSetType type,
        string[] licenceNumbers,
        Aggregate[]? aggregateSets) =>
        new()
        {
            LicenceSetType = type,
            Licences = licenceNumbers.Select(Licence).ToArray(),
            AggregateSets = aggregateSets
        };

    [Fact]
    public void WhenACandidateCoversEveryLicenceAndAddsMore_ThenItIsASuperset()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"], 100);
        var candidate = Aggregate("7/8/9", ["1/2/3", "4/5/6"], 100);

        var supersets = AbstractionLicenceSchemaConverter.GetSupersets(aggregate, [candidate]);

        Assert.Same(candidate, Assert.Single(supersets));
    }

    [Fact]
    public void WhenACandidateIsMissingOneOfTheLicences_ThenItIsNotASuperset()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"], 100);
        var candidate = Aggregate("7/8/9", ["4/5/6"], 100);

        Assert.Empty(AbstractionLicenceSchemaConverter.GetSupersets(aggregate, [candidate]));
    }

    // The candidate's licence list is built from every candidate's source licence number, not
    // just its own, so a sibling in the same call supplies the licence it is missing.
    [Fact]
    public void WhenASiblingCandidateSuppliesTheMissingLicence_ThenTheCandidateCountsAsASuperset()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"], 100);
        var candidate = Aggregate("7/8/9", ["4/5/6"], 100);
        var sibling = Aggregate("1/2/3", [], 0);

        var supersets = AbstractionLicenceSchemaConverter.GetSupersets(aggregate, [candidate, sibling]);

        Assert.Same(candidate, Assert.Single(supersets));
    }

    // The limit comparison sums every candidate rather than reading the one being judged, so a
    // candidate passes on a total it does not hold.
    [Fact]
    public void WhenTheCandidatesTotalMatches_ThenACandidateOfADifferentValuePasses()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"], 100);
        var candidate = Aggregate("7/8/9", ["1/2/3", "4/5/6"], 60);
        var sibling = Aggregate("0/0/0", [], 40);

        var supersets = AbstractionLicenceSchemaConverter.GetSupersets(aggregate, [candidate, sibling]);

        Assert.Same(candidate, Assert.Single(supersets));
        Assert.Equal(60, candidate.GetCombinedLimitValue());
    }

    // WRADI-547. GetCombinedLimitValue returns int, so summing candidates whose totals each
    // saturate at int.MaxValue overflows before any candidate is judged.
    [Fact]
    public void WhenTheCandidatesTotalsExceedIntMax_ThenItThrows()
    {
        var aggregate = Aggregate("1/2/3", ["4/5/6"], 100);

        Assert.Throws<OverflowException>(() =>
            AbstractionLicenceSchemaConverter.GetSupersets(
                aggregate,
                [
                    Aggregate("7/8/9", ["1/2/3", "4/5/6"], 5_000_000_000),
                    Aggregate("0/0/0", [], 9_000_000_000)
                ]));
    }

    [Fact]
    public void WhenACandidateSetCoversEveryLicenceAndAddsMore_ThenItIsASuperset()
    {
        var licenceSet = LicenceSet(
            LicenceSetType.LicencesGroupedByAbstractionLimits,
            ["1/2/3"],
            [Aggregate("1/2/3", [], 100)]);

        var candidate = LicenceSet(
            LicenceSetType.LicencesGroupedByAbstractionLimits,
            ["1/2/3", "4/5/6"],
            [Aggregate("1/2/3", ["4/5/6"], 100)]);

        var supersets = AbstractionLicenceSchemaConverter.GetSupersets(licenceSet, [candidate]);

        Assert.Same(candidate, Assert.Single(supersets));
    }

    [Fact]
    public void WhenTheCandidateSetIsOfAnotherType_ThenItIsNotASuperset()
    {
        var licenceSet = LicenceSet(
            LicenceSetType.LicencesGroupedByAbstractionLimits,
            ["1/2/3"],
            [Aggregate("1/2/3", [], 100)]);

        var candidate = LicenceSet(
            LicenceSetType.SingleLicenceOnly,
            ["1/2/3", "4/5/6"],
            [Aggregate("1/2/3", ["4/5/6"], 100)]);

        Assert.Empty(AbstractionLicenceSchemaConverter.GetSupersets(licenceSet, [candidate]));
    }

    [Fact]
    public void WhenTheCandidateSetHasADifferentNumberOfAggregates_ThenItIsNotASuperset()
    {
        var licenceSet = LicenceSet(
            LicenceSetType.LicencesGroupedByAbstractionLimits,
            ["1/2/3"],
            [Aggregate("1/2/3", [], 100)]);

        var candidate = LicenceSet(
            LicenceSetType.LicencesGroupedByAbstractionLimits,
            ["1/2/3", "4/5/6"],
            [Aggregate("1/2/3", ["4/5/6"], 60), Aggregate("4/5/6", [], 40)]);

        Assert.Empty(AbstractionLicenceSchemaConverter.GetSupersets(licenceSet, [candidate]));
    }

    // A missing aggregate list is compared as zero rather than as unknown, so a set with no
    // aggregates at all matches one whose aggregates total nothing.
    [Fact]
    public void WhenNeitherSetHasAggregates_ThenTheLimitComparisonPasses()
    {
        var licenceSet = LicenceSet(LicenceSetType.LicencesGroupedByAbstractionLimits, ["1/2/3"], null);

        var candidate = LicenceSet(
            LicenceSetType.LicencesGroupedByAbstractionLimits,
            ["1/2/3", "4/5/6"],
            null);

        Assert.Same(candidate, Assert.Single(
            AbstractionLicenceSchemaConverter.GetSupersets(licenceSet, [candidate])));
    }
}
