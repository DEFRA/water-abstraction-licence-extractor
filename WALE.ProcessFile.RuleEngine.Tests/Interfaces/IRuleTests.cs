using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Interfaces;

// ReSharper disable once InconsistentNaming
public class IRuleTests
{
    [Theory]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_ShouldHaveValidRuleName(IRule<FileTypeResult> rule)
    {
        // Act & Assert
        var ruleName = rule.RuleName;
        ruleName.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_ShouldHaveValidPriority(IRule<FileTypeResult> rule)
    {
        // Act & Assert
        var priority = rule.Priority;
        priority.Should().BeGreaterThan(0);
    }

    [Theory(Skip = "Needs investigation and fixing")]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_CanApply_WithNullContent_ShouldReturnFalse(IRule<FileTypeResult> rule)
    {
        var documentLine = new DocumentLine(
            0,
            0,
            [],
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
        var result = rule.CanApply(null);

        // Assert
        result.Should().BeFalse();
    }

    [Theory(Skip = "Needs investigation and fixing")]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_CanApply_WithEmptyContent_ShouldReturnFalse(IRule<FileTypeResult> rule)
    {
        // Arrange
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords(string.Empty, null))],
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
        var result = rule.CanApply(matchesResult);

        // Assert
        result.Should().BeFalse();
    }

    [Theory(Skip = "Needs investigation and fixing")]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_CanApply_WithWhitespaceContent_ShouldReturnFalse(IRule<FileTypeResult> rule)
    {
        // Arrange
        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords("   ", null))],
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
        var result = rule.CanApply(matchesResult);

        // Assert
        result.Should().BeFalse();
    }

    public static IEnumerable<object[]> GetRuleImplementations()
    {
        yield return [new AddendumFileTypeRule()];
        yield return [new LicenceFileTypeRule()];
    }
}
