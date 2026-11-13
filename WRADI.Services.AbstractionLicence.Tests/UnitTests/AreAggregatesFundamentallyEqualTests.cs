using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// AreAggregatesFundamentallyEqual decides whether two aggregates are the same aggregate, which is
/// what stops one abstraction limit being counted twice across the licences that share it. It had
/// no direct coverage because it was private.
///
/// These state current behaviour. The cross-source rule is the one that matters and the one a
/// reader would not guess from the call site.
/// </summary>
public class AreAggregatesFundamentallyEqualTests
{
    private static Aggregate Aggregate(string sourceLicence, string[]? linked, params double[] limits) =>
        new()
        {
            SourceLicenceNumber = sourceLicence,
            LinkedLicences = linked,
            Limits = limits.Select(value => new AbstractionLimit { Value = value }).ToList()
        };

    [Fact]
    public void WhenTheSourceAndLinksAndLimitsAllMatch_ThenTheyAreTheSameAggregate()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["B"], 100)));
    }

    // Limits are compared as a set, not a sequence, so the order they were extracted in does not
    // make two aggregates different.
    [Fact]
    public void WhenTheSameLimitsArriveInADifferentOrder_ThenTheyAreStillTheSameAggregate()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100, 200),
            Aggregate("A", ["B"], 200, 100)));
    }

    // The rule that earns this method its name. One aggregate reached from licence A linking to B,
    // and the other reached from B linking to A, are the same real-world aggregate: each side's
    // own source number is allowed to stand in for the other's link.
    [Fact]
    public void WhenTheSameAggregateIsSeenFromEachLicenceInTurn_ThenItIsNotCountedTwice()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("B", ["A"], 100)));
    }

    [Fact]
    public void WhenTheLimitValuesDiffer_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["B"], 999)));
    }

    [Fact]
    public void WhenOneCoversMoreLicences_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["B", "C"], 100)));
    }

    [Fact]
    public void WhenTheLinkedLicencesAreDisjoint_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["C"], 100)));
    }

    [Fact]
    public void WhenNeitherHasLinkedLicences_ThenTheLimitsAloneDecide()
    {
        Assert.True(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", null, 100),
            Aggregate("A", null, 100)));

        Assert.False(AggregateHelper.AreAggregatesFundamentallyEqual(
            Aggregate("A", null, 100),
            Aggregate("A", null, 200)));
    }
}
