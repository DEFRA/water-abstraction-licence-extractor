using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;
using Xunit;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Tests.Rules;

public class NationalRiversSecondaryTemplateConfigurationRuleTests
{
    private readonly NationalRiversSecondaryTemplateConfigurationRule _rule = new();

    private static MatchesResult WithMatch(string labelGroupName, string labelName)
    {
        return new MatchesResult
        {
            ScannedFile = true,
            Matches =
            [
                new LabelGroupResult
                {
                    LabelGroupName = labelGroupName,
                    MatchedLabel = new LabelToMatch { Name = labelName }
                }
            ]
        };
    }

    [Fact]
    public void RuleName_DoesNotCollideWithTheExistingNraRules()
    {
        _rule.RuleName.Should().Be("NRA-Scanned (Secondary Template)");
        _rule.RuleName.Should().NotBe(new NationalRiversWithoutSplitTemplateConfigurationRule().RuleName);
    }

    [Fact]
    public void Priority_SitsBelowTheExplicitNraRuleAndAboveTheEaScannedOnes()
    {
        _rule.Priority.Should().BeLessThan(new NationalRiversWithoutSplitTemplateConfigurationRule().Priority);
        _rule.Priority.Should().BeGreaterThan(new EaScannedWithoutSplitTemplateConfigurationRule().Priority);
    }

    [Theory]
    [InlineData("NRAOld", "NRAOld")]
    [InlineData("NWNRAOld", "NRAOld")]
    [InlineData("NENRAModern1", "NRAModern 1")]
    [InlineData("AnglianNRAModern2", "NRAAnglian Modern 2")]
    public void CanApply_WhenAPerRegionGroupMatchedItsRegionLabel_ClassifiesAsNraScanned(
        string labelGroupName,
        string expectedTemplate)
    {
        var content = WithMatch(labelGroupName, "Region");

        _rule.CanApply(content).Should().BeTrue();

        var result = _rule.Apply(content);
        result.TemplateType.Should().Be("NRA-Scanned");
        result.Template.Should().Be(expectedTemplate);
    }

    [Fact]
    public void CanApply_WhenOnlyTheGenericLicenceLabelMatched_DoesNotClassify()
    {
        _rule.CanApply(WithMatch("NRAOld", "Licence")).Should().BeFalse();
    }

    /// Classification must survive a database round trip or templateType could never be backfilled
    /// onto licences already extracted.
    [Fact]
    public void CanApply_OnADeserialisedResultWhereOnlyMatchedLabelNameSurvived_StillClassifies()
    {
        var content = new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    LabelGroupName = "NRAOld",
                    MatchedLabelName = "Region"
                }
            ],
            ScannedFile = true
        };

        content.Matches![0].MatchedLabel.Should().BeNull();

        _rule.CanApply(content).Should().BeTrue();
        _rule.Apply(content).Template.Should().Be("NRAOld");
    }

    [Fact]
    public void CanApply_OnANativeDocument_DoesNotClassify()
    {
        var content = WithMatch("NRAOld", "Region");
        content.ScannedFile = false;

        _rule.CanApply(content).Should().BeFalse();
    }

    [Theory]
    [InlineData("NationalRivers")]
    [InlineData("NESplitLabels")]
    public void CanApply_WhenAnotherRulesGroupAlsoMatched_StandsAside(string otherGroup)
    {
        var content = WithMatch("NRAOld", "Region");
        content.Matches!.Add(new LabelGroupResult { LabelGroupName = otherGroup });

        _rule.Region = "NE";

        _rule.CanApply(content).Should().BeFalse();
    }

    [Fact]
    public void CanApply_WithNoMatches_DoesNotClassify()
    {
        _rule.CanApply(new MatchesResult { Matches = [] }).Should().BeFalse();
        _rule.CanApply(new MatchesResult()).Should().BeFalse();
    }
}
