using FluentAssertions;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Rules.FileType;

public class AddendumFileTypeRuleTests
{
    private readonly AddendumFileTypeRule _rule;

    public AddendumFileTypeRuleTests()
    {
        _rule = new AddendumFileTypeRule();
    }

    [Fact]
    public void RuleName_ShouldReturnCorrectName()
    {
        // Assert
        _rule.RuleName.Should().Be("AddendumFileType");
    }

    [Fact]
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
        var result = _rule.CanApply(content);

        // Assert
        result.Should().BeTrue();
    }

    [Theory]
    [InlineData("The addendum is attached")]
    [InlineData("See addendum for details")]
    [InlineData("Additional terms in appendix")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void CanApply_WithoutThisAddendumContent_ShouldReturnFalse(string content)
    {
        // Act
        var result = _rule.CanApply(content);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Apply_ShouldReturnAddendumFileType()
    {
        // Arrange
        var content = "This addendum modifies the original agreement";

        // Act
        var result = _rule.Apply(content);

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

    [Fact]
    public void Apply_WithMultipleMatches_ShouldIncludeAllMatches()
    {
        // Arrange
        var content = "This addendum supersedes the previous addendum. This addendum is final.";

        // Act
        var result = _rule.Apply(content);

        // Assert
        result.MatchedTerms.Should().HaveCount(1);
        result.MatchedTerms.Should().AllBe("this addendum");
        result.Metadata["MatchCount"].Should().Be(1);
    }
}
