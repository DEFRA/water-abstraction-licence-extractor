using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// AreAggregatesFundamentallyEqual is what stops one abstraction limit being counted twice across
/// the licences that share it. These state current behaviour, not desired behaviour.
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
        Assert.True(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["B"], 100)));
    }

    [Fact]
    public void WhenTheSameLimitsArriveInADifferentOrder_ThenTheyAreStillTheSameAggregate()
    {
        Assert.True(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100, 200),
            Aggregate("A", ["B"], 200, 100)));
    }

    // Each side's own source number is allowed to stand in for the other's link, which is what
    // makes A-links-B and B-links-A one aggregate rather than two.
    [Fact]
    public void WhenTheSameAggregateIsSeenFromEachLicenceInTurn_ThenItIsNotCountedTwice()
    {
        Assert.True(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("B", ["A"], 100)));
    }

    [Fact]
    public void WhenTheLimitValuesDiffer_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["B"], 999)));
    }

    [Fact]
    public void WhenOneCoversMoreLicences_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["B", "C"], 100)));
    }

    [Fact]
    public void WhenTheLinkedLicencesAreDisjoint_ThenTheyAreDifferentAggregates()
    {
        Assert.False(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", ["B"], 100),
            Aggregate("A", ["C"], 100)));
    }

    [Fact]
    public void WhenNeitherHasLinkedLicences_ThenTheLimitsAloneDecide()
    {
        Assert.True(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", null, 100),
            Aggregate("A", null, 100)));

        Assert.False(AbstractionLicenceSchemaConverter.AreAggregatesFundamentallyEqual(
            Aggregate("A", null, 100),
            Aggregate("A", null, 200)));
    }
}
