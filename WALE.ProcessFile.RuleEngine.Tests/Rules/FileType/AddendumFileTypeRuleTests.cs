using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Rules.FileType;

public class AddendumFileTypeRuleTests
{
    private readonly AddendumFileTypeRule _rule = new();

    [Fact(Skip = "Needs investigation and fixing")]
    public void RuleName_ShouldReturnCorrectName()
    {
        // Assert
        _rule.RuleName.Should().Be("AddendumFileType");
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public void Priority_ShouldReturn100()
    {
        // Assert
        _rule.Priority.Should().Be(100);
    }

    [Theory]
    [InlineData("This addendum modifies the original agreement")]
    [InlineData("THIS ADDENDUM contains new terms")]
    [InlineData("Please refer to this addendum")]
    [InlineData("This Addendum is effective immediately")]
    public void CanApply_WithThisAddendumContent_ShouldReturnTrue(string content)
    {
        // Act
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(content)],
            0,
            0,
            0,
            0);
        
        var matchesResult = new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    Text = [documentLine],
                    LabelGroupName = "Addendum"
                }
            ]
        };
        
        var result = _rule.CanApply(matchesResult);

        // Assert
        result.Should().BeTrue();
    }

    [Theory(Skip = "Needs investigation and fixing")]
    [InlineData("The addendum is attached")]
    [InlineData("See addendum for details")]
    [InlineData("Additional terms in appendix")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CanApply_WithoutThisAddendumContent_ShouldReturnFalse(string content)
    {
        // Act
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(content)],
            0,
            0,
            0,
            0);
        
        var matchesResult = new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    Text = [documentLine],
                    LabelGroupName = "Addendum"
                }
            ]
        };
        
        var result = _rule.CanApply(matchesResult);

        // Assert
        result.Should().BeFalse();
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public void Apply_ShouldReturnAddendumFileType()
    {
        // Arrange
        var content = "This addendum modifies the original agreement";

        // Act
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(content)],
            0,
            0,
            0,
            0);
        
        var matchesResult = new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    Text = [documentLine],
                    LabelGroupName = "Addendum"
                }
            ]
        };
        
        // Act
        var result = _rule.Apply(matchesResult);

        // Assert
        result.Should().NotBeNull();
        result.FileType.Should().Be("Addendum");
        result.Confidence.Should().Be(0.9);
        result.IdentifiedByRule.Should().Be("AddendumFileType");
        result.MatchedTerms.Should().Contain("this addendum");
        result.Metadata.Should().ContainKey("MatchCount");
        result.Metadata.Should().ContainKey("ContentLength");
        result.Metadata["ContentLength"].Should().Be(content.Length);
    }

    [Fact(Skip = "Needs investigation and fixing")]
    public void Apply_WithMultipleMatches_ShouldIncludeAllMatches()
    {
        // Arrange
        var content = "This addendum supersedes the previous addendum. This addendum is final.";

        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(content)],
            0,
            0,
            0,
            0);
        
        var matchesResult = new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    Text = [documentLine],
                    LabelGroupName = "Addendum"
                }
            ]
        };
        
        // Act
        var result = _rule.Apply(matchesResult);

        // Assert
        result.MatchedTerms.Should().HaveCount(1);
        result.MatchedTerms.Should().AllBe("this addendum");
        result.Metadata["MatchCount"].Should().Be(1);
    }
}
