using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// The three assertions that guard a licence's aggregates. They throw rather than return, so a
/// violation fails the file. These state current behaviour, not desired behaviour.
/// </summary>
public class AggregateInvariantTests
{
    private static Aggregate Aggregate(string sourceLicence) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = [],
        Limits = [new AbstractionLimit { Value = 100 }]
    };

    private static AggregateVariation Variation(string sourceLicence) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = [],
        Limits = [new AbstractionLimit { Value = 100 }]
    };

    private static Licence Licence(ScrapeStatus status, params Aggregate[] aggregates) => new()
    {
        Status = status,
        AbstractionLimits = new AbstractionLimits { Aggregates = aggregates }
    };

    [Fact]
    public void WhenAnOkLicenceHasTwoAggregatesWithTheSameId_ThenItThrows()
    {
        Assert.Throws<Exception>(() =>
            AbstractionLicenceSchemaConverter.AssertNoDuplicateAggregates(
                [Licence(ScrapeStatus.Ok, Aggregate("1/2/3"), Aggregate("1/2/3"))]));
    }

    [Fact]
    public void WhenTheAggregatesDiffer_ThenItPasses()
    {
        AbstractionLicenceSchemaConverter.AssertNoDuplicateAggregates(
            [Licence(ScrapeStatus.Ok, Aggregate("1/2/3"), Aggregate("4/5/6"))]);
    }

    [Fact]
    public void WhenALicenceHasNoAggregates_ThenItPasses()
    {
        AbstractionLicenceSchemaConverter.AssertNoDuplicateAggregates(
        [
            new Licence
            {
                Status = ScrapeStatus.Ok,
                AbstractionLimits = new AbstractionLimits { Aggregates = null }
            }
        ]);
    }

    [Fact]
    public void WhenAnOkAggregateHasTwoVariationsWithTheSameId_ThenItThrows()
    {
        var aggregate = Aggregate("1/2/3");
        aggregate.Variations = [Variation("9/9/9"), Variation("9/9/9")];

        Assert.Throws<Exception>(() =>
            AbstractionLicenceSchemaConverter.AssertNoDuplicateVariations(
                [Licence(ScrapeStatus.Ok, aggregate)]));
    }

    // Both duplicate checks skip licences that did not extract cleanly, so a failed licence may
    // carry duplicates without failing the run.
    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenDuplicatesAreIgnored(ScrapeStatus status)
    {
        var aggregate = Aggregate("1/2/3");
        aggregate.Variations = [Variation("9/9/9"), Variation("9/9/9")];

        AbstractionLicenceSchemaConverter.AssertNoDuplicateAggregates(
            [Licence(status, Aggregate("1/2/3"), Aggregate("1/2/3"))]);

        AbstractionLicenceSchemaConverter.AssertNoDuplicateVariations([Licence(status, aggregate)]);
    }

    [Fact]
    public void WhenAVariationCarriesItsOwnVariations_ThenItThrows()
    {
        var nested = Variation("9/9/9");
        nested.Variations = [Variation("8/8/8")];

        var aggregate = Aggregate("1/2/3");
        aggregate.Variations = [nested];

        Assert.Throws<Exception>(() =>
            AbstractionLicenceSchemaConverter.AssertNoMultipleLevelVariations(
                [Licence(ScrapeStatus.Ok, aggregate)]));
    }

    // The odd one out: unlike the two duplicate checks, this one has no status filter, so a
    // licence that failed extraction can still fail the run on nesting alone.
    [Fact]
    public void WhenALicenceDidNotExtractCleanly_ThenNestedVariationsStillThrow()
    {
        var nested = Variation("9/9/9");
        nested.Variations = [Variation("8/8/8")];

        var aggregate = Aggregate("1/2/3");
        aggregate.Variations = [nested];

        Assert.Throws<Exception>(() =>
            AbstractionLicenceSchemaConverter.AssertNoMultipleLevelVariations(
                [Licence(ScrapeStatus.Error, aggregate)]));
    }

    [Fact]
    public void WhenAVariationHasAnEmptyVariationsArray_ThenItPasses()
    {
        var empty = Variation("9/9/9");
        empty.Variations = [];

        var aggregate = Aggregate("1/2/3");
        aggregate.Variations = [empty];

        AbstractionLicenceSchemaConverter.AssertNoMultipleLevelVariations(
            [Licence(ScrapeStatus.Ok, aggregate)]);
    }
}
