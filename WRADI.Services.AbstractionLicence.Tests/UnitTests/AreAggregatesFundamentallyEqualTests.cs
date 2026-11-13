using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Deciding that two aggregates are the same one is what stops an abstraction limit being
/// counted twice across the licences that share it.
/// </summary>
public class AreAggregatesFundamentallyEqualTests
{
    [Fact]
    public void WhenTheSourceAndLinksAndLimitsAllMatch_ThenTheyAreTheSameAggregate()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(100).Build(),
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(100).Build()));
    }

    // Limits are compared as a set, not a sequence, so the order they were extracted in does not
    // make two aggregates different.
    [Fact]
    public void WhenTheSameLimitsArriveInADifferentOrder_ThenTheyAreStillTheSameAggregate()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(100, 200).Build(),
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(200, 100).Build()));
    }

    // The rule that earns this method its name. One aggregate reached from licence A linking to B,
    // and the other reached from B linking to A, are the same real-world aggregate: each side's
    // own source number is allowed to stand in for the other's link.
    [Fact]
    public void WhenTheSameAggregateIsSeenFromEachLicenceInTurn_ThenItIsNotCountedTwice()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(100).Build(),
            AggregateBuilder.An().For("B").LinkedTo("A").Limited(100).Build()));
    }

    [Fact]
    public void WhenTheLimitValuesDiffer_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(100).Build(),
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(999).Build()));
    }

    [Fact]
    public void WhenOneCoversMoreLicences_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(100).Build(),
            AggregateBuilder.An().For("A").LinkedTo("B", "C").Limited(100).Build()));
    }

    [Fact]
    public void WhenTheLinkedLicencesAreDisjoint_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").LinkedTo("B").Limited(100).Build(),
            AggregateBuilder.An().For("A").LinkedTo("C").Limited(100).Build()));
    }

    [Fact]
    public void WhenNeitherHasLinkedLicences_ThenTheLimitsAloneDecide()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").WithNullLinkedLicences().Limited(100).Build(),
            AggregateBuilder.An().For("A").WithNullLinkedLicences().Limited(100).Build()));

        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            AggregateBuilder.An().For("A").WithNullLinkedLicences().Limited(100).Build(),
            AggregateBuilder.An().For("A").WithNullLinkedLicences().Limited(200).Build()));
    }
}
