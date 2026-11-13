using WALE.ProcessFile.Core.Enums;
using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// The three assertions that guard a licence's aggregates. They throw rather than return, so a
/// violation fails the file. These state current behaviour, not desired behaviour.
/// </summary>
public class AggregateInvariantTests
{
    [Fact]
    public void WhenAnOkLicenceHasTwoAggregatesWithTheSameId_ThenItThrows()
    {
        Assert.Throws<Exception>(() =>
            AggregateAssertions.AssertNoDuplicateAggregates(
            [
                LicenceBuilder.A()
                    .Holding(
                        AggregateBuilder.An().For("1/2/3").Build(),
                        AggregateBuilder.An().For("1/2/3").Build())
                    .Build()
            ]));
    }

    [Fact]
    public void WhenTheAggregatesDiffer_ThenItPasses()
    {
        AggregateAssertions.AssertNoDuplicateAggregates(
        [
            LicenceBuilder.A()
                .Holding(
                    AggregateBuilder.An().For("1/2/3").Build(),
                    AggregateBuilder.An().For("4/5/6").Build())
                .Build()
        ]);
    }

    [Fact]
    public void WhenALicenceHasNoAggregates_ThenItPasses()
    {
        AggregateAssertions.AssertNoDuplicateAggregates([LicenceBuilder.A().Build()]);
    }

    [Fact]
    public void WhenAnOkAggregateHasTwoVariationsWithTheSameId_ThenItThrows()
    {
        var aggregate = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(
                AggregateBuilder.An().For("9/9/9").BuildVariation(),
                AggregateBuilder.An().For("9/9/9").BuildVariation())
            .Build();

        Assert.Throws<Exception>(() =>
            AggregateAssertions.AssertNoDuplicateVariations(
                [LicenceBuilder.A().Holding(aggregate).Build()]));
    }

    // Both duplicate checks skip licences that did not extract cleanly, so a failed licence may
    // carry duplicates without failing the run.
    [Theory]
    [InlineData(ScrapeStatus.Error)]
    [InlineData(ScrapeStatus.TransientError)]
    public void WhenTheLicenceDidNotExtractCleanly_ThenDuplicatesAreIgnored(ScrapeStatus status)
    {
        var aggregate = AggregateBuilder.An()
            .For("1/2/3")
            .Varying(
                AggregateBuilder.An().For("9/9/9").BuildVariation(),
                AggregateBuilder.An().For("9/9/9").BuildVariation())
            .Build();

        AggregateAssertions.AssertNoDuplicateAggregates(
        [
            LicenceBuilder.A()
                .ThatDidNotExtractCleanly(status)
                .Holding(
                    AggregateBuilder.An().For("1/2/3").Build(),
                    AggregateBuilder.An().For("1/2/3").Build())
                .Build()
        ]);

        AggregateAssertions.AssertNoDuplicateVariations(
        [
            LicenceBuilder.A().ThatDidNotExtractCleanly(status).Holding(aggregate).Build()
        ]);
    }

    [Fact]
    public void WhenAVariationCarriesItsOwnVariations_ThenItThrows()
    {
        var nested = AggregateBuilder.An()
            .For("9/9/9")
            .Varying(AggregateBuilder.An().For("8/8/8").BuildVariation())
            .BuildVariation();

        var aggregate = AggregateBuilder.An().For("1/2/3").Varying(nested).Build();

        Assert.Throws<Exception>(() =>
            AggregateAssertions.AssertNoMultipleLevelVariations(
                [LicenceBuilder.A().Holding(aggregate).Build()]));
    }

    // The odd one out: unlike the two duplicate checks, this one has no status filter, so a
    // licence that failed extraction can still fail the run on nesting alone.
    [Fact]
    public void WhenALicenceDidNotExtractCleanly_ThenNestedVariationsStillThrow()
    {
        var nested = AggregateBuilder.An()
            .For("9/9/9")
            .Varying(AggregateBuilder.An().For("8/8/8").BuildVariation())
            .BuildVariation();

        var aggregate = AggregateBuilder.An().For("1/2/3").Varying(nested).Build();

        Assert.Throws<Exception>(() =>
            AggregateAssertions.AssertNoMultipleLevelVariations(
            [
                LicenceBuilder.A()
                    .ThatDidNotExtractCleanly(ScrapeStatus.Error)
                    .Holding(aggregate)
                    .Build()
            ]));
    }

    [Fact]
    public void WhenAVariationHasAnEmptyVariationsArray_ThenItPasses()
    {
        var empty = AggregateBuilder.An().For("9/9/9").Varying().BuildVariation();
        var aggregate = AggregateBuilder.An().For("1/2/3").Varying(empty).Build();

        AggregateAssertions.AssertNoMultipleLevelVariations(
            [LicenceBuilder.A().Holding(aggregate).Build()]);
    }
}
