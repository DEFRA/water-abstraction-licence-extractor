using WRADI.Core.AbstractionLicence.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Which of a set of candidates counts as covering a given aggregate or licence set. These state
/// current behaviour, not desired behaviour.
/// </summary>
public class GetSupersetsTests
{
    private static LicenceSetBuilder LicenceSetOf(params string[] licenceNumbers) =>
        LicenceSetBuilder.A()
            .Containing(licenceNumbers
                .Select(licenceNumber => LicenceBuilder.A().Numbered(licenceNumber).Build())
                .ToArray());

    [Fact]
    public void WhenACandidateCoversEveryLicenceAndAddsMore_ThenItIsASuperset()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var candidate = AggregateBuilder.An().For("7/8/9").LinkedTo("1/2/3", "4/5/6").Build();

        var supersets = AggregateHelper.GetSupersets(aggregate, [candidate]);

        Assert.Same(candidate, Assert.Single(supersets));
    }

    [Fact]
    public void WhenACandidateIsMissingOneOfTheLicences_ThenItIsNotASuperset()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var candidate = AggregateBuilder.An().For("7/8/9").LinkedTo("4/5/6").Build();

        Assert.Empty(AggregateHelper.GetSupersets(aggregate, [candidate]));
    }

    // The candidate's licence list is built from every candidate's source licence number, not
    // just its own, so a sibling in the same call supplies the licence it is missing.
    [Fact]
    public void WhenASiblingCandidateSuppliesTheMissingLicence_ThenTheCandidateCountsAsASuperset()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();
        var candidate = AggregateBuilder.An().For("7/8/9").LinkedTo("4/5/6").Build();
        var sibling = AggregateBuilder.An().For("1/2/3").Limited(0).Build();

        var supersets = AggregateHelper.GetSupersets(aggregate, [candidate, sibling]);

        Assert.Same(candidate, Assert.Single(supersets));
    }

    // The limit comparison sums every candidate rather than reading the one being judged, so a
    // candidate passes on a total it does not hold.
    [Fact]
    public void WhenTheCandidatesTotalMatches_ThenACandidateOfADifferentValuePasses()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        var candidate = AggregateBuilder.An()
            .For("7/8/9").LinkedTo("1/2/3", "4/5/6").Limited(60).Build();

        var sibling = AggregateBuilder.An().For("0/0/0").Limited(40).Build();

        var supersets = AggregateHelper.GetSupersets(aggregate, [candidate, sibling]);

        Assert.Same(candidate, Assert.Single(supersets));
        Assert.Equal(60, candidate.GetCombinedLimitValue());
    }

    // WRADI-547. GetCombinedLimitValue returns int, so summing candidates whose totals each
    // saturate at int.MaxValue overflows before any candidate is judged.
    [Fact]
    public void WhenTheCandidatesTotalsExceedIntMax_ThenItThrows()
    {
        var aggregate = AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build();

        Assert.Throws<OverflowException>(() =>
            AggregateHelper.GetSupersets(
                aggregate,
                [
                    AggregateBuilder.An()
                        .For("7/8/9").LinkedTo("1/2/3", "4/5/6").Limited(5_000_000_000).Build(),
                    AggregateBuilder.An().For("0/0/0").Limited(9_000_000_000).Build()
                ]));
    }

    [Fact]
    public void WhenACandidateSetCoversEveryLicenceAndAddsMore_ThenItIsASuperset()
    {
        var licenceSet = LicenceSetOf("1/2/3")
            .WithAggregateSets(AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        var candidate = LicenceSetOf("1/2/3", "4/5/6")
            .WithAggregateSets(AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build())
            .Build();

        var supersets = AggregateHelper.GetSupersets(licenceSet, [candidate]);

        Assert.Same(candidate, Assert.Single(supersets));
    }

    [Fact]
    public void WhenTheCandidateSetIsOfAnotherType_ThenItIsNotASuperset()
    {
        var licenceSet = LicenceSetOf("1/2/3")
            .WithAggregateSets(AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        var candidate = LicenceSetOf("1/2/3", "4/5/6")
            .OfType(LicenceSetType.SingleLicenceOnly)
            .WithAggregateSets(AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build())
            .Build();

        Assert.Empty(AggregateHelper.GetSupersets(licenceSet, [candidate]));
    }

    [Fact]
    public void WhenTheCandidateSetHasADifferentNumberOfAggregates_ThenItIsNotASuperset()
    {
        var licenceSet = LicenceSetOf("1/2/3")
            .WithAggregateSets(AggregateBuilder.An().For("1/2/3").Build())
            .Build();

        var candidate = LicenceSetOf("1/2/3", "4/5/6")
            .WithAggregateSets(
                AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Limited(60).Build(),
                AggregateBuilder.An().For("4/5/6").Limited(40).Build())
            .Build();

        Assert.Empty(AggregateHelper.GetSupersets(licenceSet, [candidate]));
    }

    // A missing aggregate list is compared as zero rather than as unknown, so a set with no
    // aggregates at all matches one whose aggregates total nothing.
    [Fact]
    public void WhenNeitherSetHasAggregates_ThenTheLimitComparisonPasses()
    {
        var licenceSet = LicenceSetOf("1/2/3").Build();
        var candidate = LicenceSetOf("1/2/3", "4/5/6").Build();

        Assert.Same(candidate, Assert.Single(AggregateHelper.GetSupersets(licenceSet, [candidate])));
    }
}
