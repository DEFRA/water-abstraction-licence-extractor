using FluentAssertions;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Interfaces;

public class IRuleTests
{
    [Theory]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_ShouldHaveValidRuleName(object ruleObj)
    {
        // Arrange
        var rule = (dynamic)ruleObj;

        // Act & Assert
        string ruleName = rule.RuleName;
        ruleName.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_ShouldHaveValidPriority(object ruleObj)
    {
        // Arrange
        var rule = (dynamic)ruleObj;

        // Act & Assert
        int priority = rule.Priority;
        priority.Should().BeGreaterThan(0);
    }

    [Theory]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_CanApply_WithNullContent_ShouldReturnFalse(object ruleObj)
    {
        // Arrange
        var rule = (dynamic)ruleObj;

        // Act
        bool result = rule.CanApply(null);

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_CanApply_WithEmptyContent_ShouldReturnFalse(object ruleObj)
    {
        // Arrange
        var rule = (dynamic)ruleObj;

        // Act
        bool result = rule.CanApply("");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [MemberData(nameof(GetRuleImplementations))]
    public void Rule_CanApply_WithWhitespaceContent_ShouldReturnFalse(object ruleObj)
    {
        // Arrange
        var rule = (dynamic)ruleObj;

        // Act
        bool result = rule.CanApply("   ");

        // Assert
        result.Should().BeFalse();
    }

    public static IEnumerable<object[]> GetRuleImplementations()
    {
        yield return new object[] { new AddendumFileTypeRule() };
        yield return new object[] { new LicenceFileTypeRule() };
    }
}
