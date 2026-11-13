using WALE.ProcessFile.Core.Enums;
using WRADI.Core.AbstractionLicence.Enums;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Converters;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Gathering a group of licences' aggregates into one list for the set that holds them. These
/// state current behaviour, not desired behaviour.
/// </summary>
public class GetAggregatesTests
{
    private static Aggregate Aggregate(string sourceLicence, string[] linked) => new()
    {
        SourceLicenceNumber = sourceLicence,
        PrimaryType = PrimaryType.LicenceToLicence,
        SubType = SubType.PointToPoint,
        LinkedLicences = linked,
        Limits = [new AbstractionLimit { Value = 100 }]
    };

    private static Licence Licence(string licenceNumber, Aggregate[]? aggregates) => new()
    {
        Status = ScrapeStatus.Ok,
        LicenceNumber = new ValueWithConfidence<string>(licenceNumber, null, null),
        AbstractionLimits = new AbstractionLimits { Aggregates = aggregates }
    };

    [Fact]
    public void WhenSeveralLicencesHoldAggregates_ThenAllOfThemComeBack()
    {
        var aggregates = AbstractionLicenceSchemaConverter.GetAggregates(
        [
            Licence("1/2/3", [Aggregate("1/2/3", [])]),
            Licence("4/5/6", [Aggregate("4/5/6", [])])
        ],
            excludeAnyLinksNotInSet: false);

        Assert.Equal(2, aggregates!.Length);
    }

    [Fact]
    public void WhenNoLicenceHoldsAnAggregate_ThenNullComesBackRatherThanAnEmptyArray()
    {
        Assert.Null(AbstractionLicenceSchemaConverter.GetAggregates(
            [Licence("1/2/3", null)],
            excludeAnyLinksNotInSet: false));
    }

    [Fact]
    public void WhenAnAggregateIsReturned_ThenItIsACopyRatherThanTheLicencesOwn()
    {
        var held = Aggregate("1/2/3", []);

        var returned = Assert.Single(AbstractionLicenceSchemaConverter.GetAggregates(
            [Licence("1/2/3", [held])],
            excludeAnyLinksNotInSet: false)!);

        Assert.NotSame(held, returned);
        Assert.Equal(held.Id, returned.Id);
    }

    [Fact]
    public void WhenTheHeldAggregateNamesALicenceSet_ThenTheCopyDoesNot()
    {
        var held = Aggregate("1/2/3", []);
        held.LicenceSetId = "123-456";

        var returned = Assert.Single(AbstractionLicenceSchemaConverter.GetAggregates(
            [Licence("1/2/3", [held])],
            excludeAnyLinksNotInSet: false)!);

        Assert.Null(returned.LicenceSetId);
        Assert.Equal("123-456", held.LicenceSetId);
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreExcluded_ThenAnAggregateNamingAnAbsentLicenceIsDropped()
    {
        Assert.Null(AbstractionLicenceSchemaConverter.GetAggregates(
            [Licence("1/2/3", [Aggregate("1/2/3", ["4/5/6"])])],
            excludeAnyLinksNotInSet: true));
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreExcluded_ThenAnAggregateNamingOnlyPresentLicencesIsKept()
    {
        var aggregates = AbstractionLicenceSchemaConverter.GetAggregates(
        [
            Licence("1/2/3", [Aggregate("1/2/3", ["4/5/6"])]),
            Licence("4/5/6", null)
        ],
            excludeAnyLinksNotInSet: true);

        Assert.Single(aggregates!);
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreExcluded_ThenAnAggregateWithNoLinksIsStillKept()
    {
        var aggregates = AbstractionLicenceSchemaConverter.GetAggregates(
            [Licence("1/2/3", [Aggregate("1/2/3", [])])],
            excludeAnyLinksNotInSet: true);

        Assert.Single(aggregates!);
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreNotExcluded_ThenAnAggregateNamingAnAbsentLicenceIsKept()
    {
        var aggregates = AbstractionLicenceSchemaConverter.GetAggregates(
            [Licence("1/2/3", [Aggregate("1/2/3", ["4/5/6"])])],
            excludeAnyLinksNotInSet: false);

        Assert.Single(aggregates!);
    }
}
