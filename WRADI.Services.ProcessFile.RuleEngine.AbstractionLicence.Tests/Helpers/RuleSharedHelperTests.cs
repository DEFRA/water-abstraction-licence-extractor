using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Helpers;
using Xunit;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Tests.Helpers;

public class RuleSharedHelperTests
{
    private static MatchesResult Matched(params (string Group, string Label)[] matches)
    {
        return new MatchesResult
        {
            Matches = matches
                .Select(m => new LabelGroupResult { LabelGroupName = m.Group, MatchedLabelName = m.Label })
                .ToList()
        };
    }

    [Theory]
    [InlineData("NRAOld", "Old")]
    [InlineData("NENRAModern1", "Modern 1")]
    [InlineData("NWNRAModern1", "NW Modern 1")]
    public void ResolvesAGroupThatMatchedItsRegionLabel(string group, string expected)
    {
        RuleSharedHelper.DetermineSecondaryTemplate(Matched((group, "Region"))).Should().Be(expected);
    }

    [Fact]
    public void ResolvesNothingWithoutARegionMatch()
    {
        RuleSharedHelper.DetermineSecondaryTemplate(null).Should().BeEmpty();
        RuleSharedHelper.DetermineSecondaryTemplate(new MatchesResult()).Should().BeEmpty();
        RuleSharedHelper.DetermineSecondaryTemplate(Matched(("NRAOld", "Licence"))).Should().BeEmpty();
    }

    [Fact]
    public void WhereRegionPatternsDifferTheMostSpecificWins()
    {
        var content = Matched(
            ("NENRAModern1", "Region"),
            ("NENRAModern2", "Region"),
            ("NRAOld", "Region"),
            ("NENRAModern2", "Licence"));

        RuleSharedHelper.DetermineSecondaryTemplate(content).Should().Be("Modern 1");
    }

    [Theory]
    [InlineData("AnglianNRAModern1", "Anglian Modern 1")]
    [InlineData("AnglianNRAModern2", "Anglian Modern 2")]
    public void TheAnglianPairIsSeparatedByWhicheverMatchedItsLicenceLabel(
        string groupWithLicence,
        string expected)
    {
        var content = Matched(
            ("AnglianNRAModern1", "Region"),
            ("AnglianNRAModern2", "Region"),
            (groupWithLicence, "Licence"));

        RuleSharedHelper.DetermineSecondaryTemplate(content).Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TheAnglianPairFallsBackToOrderWhenTheLicenceLabelCannotDecide(bool bothMatched)
    {
        var matches = new List<(string, string)>
        {
            ("AnglianNRAModern1", "Region"),
            ("AnglianNRAModern2", "Region")
        };

        if (bothMatched)
        {
            matches.Add(("AnglianNRAModern1", "Licence"));
            matches.Add(("AnglianNRAModern2", "Licence"));
        }

        RuleSharedHelper.DetermineSecondaryTemplate(Matched(matches.ToArray()))
            .Should().Be("Anglian Modern 1");
    }
}
