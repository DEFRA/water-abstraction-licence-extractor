using WRADI.DocumentType.AbstractionLicence.Helpers;
using WRADI.Services.AbstractionLicence.Tests.Helper;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

/// <summary>
/// Gathering a group of licences' aggregates into one list for the set that holds them.
/// </summary>
public class GetAggregatesTests
{
    [Fact]
    public void WhenSeveralLicencesHoldAggregates_ThenAllOfThemComeBack()
    {
        var aggregates = AggregateHelper.GetAggregates(
        [
            LicenceBuilder.A().Numbered("1/2/3")
                .Holding(AggregateBuilder.An().For("1/2/3").Build()).Build(),
            LicenceBuilder.A().Numbered("4/5/6")
                .Holding(AggregateBuilder.An().For("4/5/6").Build()).Build()
        ],
            excludeAnyLinksNotInSet: false);

        Assert.Equal(2, aggregates!.Length);
    }

    [Fact]
    public void WhenNoLicenceHoldsAnAggregate_ThenNullComesBackRatherThanAnEmptyArray()
    {
        Assert.Null(AggregateHelper.GetAggregates(
            [LicenceBuilder.A().Numbered("1/2/3").Build()],
            excludeAnyLinksNotInSet: false));
    }

    [Fact]
    public void WhenAnAggregateIsReturned_ThenItIsACopyRatherThanTheLicencesOwn()
    {
        var held = AggregateBuilder.An().For("1/2/3").Build();

        var returned = Assert.Single(AggregateHelper.GetAggregates(
            [LicenceBuilder.A().Numbered("1/2/3").Holding(held).Build()],
            excludeAnyLinksNotInSet: false)!);

        Assert.NotSame(held, returned);
        Assert.Equal(held.Id, returned.Id);
    }

    [Fact]
    public void WhenTheHeldAggregateNamesALicenceSet_ThenTheCopyDoesNot()
    {
        var held = AggregateBuilder.An().For("1/2/3").InLicenceSet("123-456").Build();

        var returned = Assert.Single(AggregateHelper.GetAggregates(
            [LicenceBuilder.A().Numbered("1/2/3").Holding(held).Build()],
            excludeAnyLinksNotInSet: false)!);

        Assert.Null(returned.LicenceSetId);
        Assert.Equal("123-456", held.LicenceSetId);
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreExcluded_ThenAnAggregateNamingAnAbsentLicenceIsDropped()
    {
        Assert.Null(AggregateHelper.GetAggregates(
        [
            LicenceBuilder.A().Numbered("1/2/3")
                .Holding(AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build()).Build()
        ],
            excludeAnyLinksNotInSet: true));
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreExcluded_ThenAnAggregateNamingOnlyPresentLicencesIsKept()
    {
        var aggregates = AggregateHelper.GetAggregates(
        [
            LicenceBuilder.A().Numbered("1/2/3")
                .Holding(AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build()).Build(),
            LicenceBuilder.A().Numbered("4/5/6").Build()
        ],
            excludeAnyLinksNotInSet: true);

        Assert.Single(aggregates!);
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreExcluded_ThenAnAggregateWithNoLinksIsStillKept()
    {
        var aggregates = AggregateHelper.GetAggregates(
        [
            LicenceBuilder.A().Numbered("1/2/3")
                .Holding(AggregateBuilder.An().For("1/2/3").Build()).Build()
        ],
            excludeAnyLinksNotInSet: true);

        Assert.Single(aggregates!);
    }

    [Fact]
    public void WhenLinksOutsideTheSetAreNotExcluded_ThenAnAggregateNamingAnAbsentLicenceIsKept()
    {
        var aggregates = AggregateHelper.GetAggregates(
        [
            LicenceBuilder.A().Numbered("1/2/3")
                .Holding(AggregateBuilder.An().For("1/2/3").LinkedTo("4/5/6").Build()).Build()
        ],
            excludeAnyLinksNotInSet: false);

        Assert.Single(aggregates!);
    }
}
