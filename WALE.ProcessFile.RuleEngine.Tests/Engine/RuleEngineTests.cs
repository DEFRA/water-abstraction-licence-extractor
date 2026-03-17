using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.RuleEngine.Engine;
using WALE.ProcessFile.RuleEngine.Interfaces;
using WALE.ProcessFile.RuleEngine.Models;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Engine;

public class RuleEngineTests
{
    [Fact]
    public void AddRule_ShouldAddRuleSuccessfully()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var rule = new TestRule("TestRule", 100, "test content", new FileTypeResult { FileType = "Test" });

        // Act
        ruleEngine.AddRule(rule);

        // Assert
        var rules = ruleEngine.GetRules();
        rules.Should().HaveCount(1);
        rules.First().RuleName.Should().Be("TestRule");
    }

    [Fact]
    public void AddRule_WithSameName_ShouldReplaceExistingRule()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var rule1 = new TestRule("TestRule", 50, "test", new FileTypeResult { FileType = "Test1" });
        var rule2 = new TestRule("TestRule", 100, "test", new FileTypeResult { FileType = "Test2" });

        // Act
        ruleEngine.AddRule(rule1);
        ruleEngine.AddRule(rule2);

        // Assert
        var rules = ruleEngine.GetRules();
        rules.Should().HaveCount(1);
        rules.First().Priority.Should().Be(100);
    }

    [Fact]
    public void AddRule_ShouldSortByPriority()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var lowPriorityRule = new TestRule("LowRule", 50, "test", new FileTypeResult { FileType = "Low" });
        var highPriorityRule = new TestRule("HighRule", 100, "test", new FileTypeResult { FileType = "High" });

        // Act
        ruleEngine.AddRule(lowPriorityRule);
        ruleEngine.AddRule(highPriorityRule);

        // Assert
        var rules = ruleEngine.GetRules().ToList();
        rules[0].Priority.Should().Be(100);
        rules[1].Priority.Should().Be(50);
    }

    [Fact]
    public void AddRule_WithNullRule_ShouldThrowArgumentNullException()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => ruleEngine.AddRule(null!));
    }

    /*[Fact]
    public void RemoveRule_ExistingRule_ShouldReturnTrue()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var rule = new TestRule("TestRule", 100, "test", new FileTypeResult { FileType = "Test" });
        ruleEngine.AddRule(rule);

        // Act
        var result = ruleEngine.RemoveRule("TestRule");

        // Assert
        result.Should().BeTrue();
        ruleEngine.GetRules().Should().BeEmpty();
    }

    [Fact]
    public void RemoveRule_NonExistingRule_ShouldReturnFalse()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();

        // Act
        var result = ruleEngine.RemoveRule("NonExistingRule");

        // Assert
        result.Should().BeFalse();
    }*/

    [Fact]
    public void Evaluate_WithApplicableRule_ShouldReturnResult()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var expectedResult = new FileTypeResult { FileType = "Schedule" };
        var rule = new TestRule("ScheduleRule", 100, "change in", expectedResult);
        ruleEngine.AddRule(rule);

        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords("This document contains change in conditions", null))],
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
        var result = ruleEngine.Evaluate(matchesResult);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("Schedule");
    }

    [Fact]
    public void Evaluate_WithNoApplicableRule_ShouldReturnDefault()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var rule = new TestRule("ScheduleRule", 100, "change in", new FileTypeResult { FileType = "Schedule" });
        ruleEngine.AddRule(rule);

        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords("This document contains no matching terms", null))],
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
        var result = ruleEngine.Evaluate(matchesResult);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void EvaluateAll_WithMultipleApplicableRules_ShouldReturnAllResults()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var rule1 = new TestRule("Rule1", 100, "test", new FileTypeResult { FileType = "Type1" });
        var rule2 = new TestRule("Rule2", 90, "test", new FileTypeResult { FileType = "Type2" });
        ruleEngine.AddRule(rule1);
        ruleEngine.AddRule(rule2);

        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords("This is a test document", null))],
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
        var results = ruleEngine.EvaluateAll(matchesResult).ToList();

        // Assert
        results.Should().HaveCount(2);
        results.Should().Contain(r => r.FileType == "Type1");
        results.Should().Contain(r => r.FileType == "Type2");
    }

    [Fact]
    public void Evaluate_WithHigherPriorityRule_ShouldReturnFirstApplicableRule()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var highPriorityRule = new TestRule("HighRule", 100, "test", new FileTypeResult { FileType = "High" });
        var lowPriorityRule = new TestRule("LowRule", 50, "test", new FileTypeResult { FileType = "Low" });
        ruleEngine.AddRule(lowPriorityRule);
        ruleEngine.AddRule(highPriorityRule);

        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords("This is a test document", null))],
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
        var result = ruleEngine.Evaluate(matchesResult);

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("High");
    }
}

// Test helper class
public class TestRule(string ruleName, int priority, string triggerContent, FileTypeResult result)
    : IRule<FileTypeResult>
{
    public string RuleName { get; } = ruleName;
    public string? Region { get; set; }
    public int Priority { get; } = priority;

    public bool CanApply(MatchesResult matchesResult)
    {
        var content = matchesResult.Matches!.Count > 0
            ? matchesResult.Matches[0].Text!.FirstOrDefault()?.Text
            : null;
        
        return !string.IsNullOrWhiteSpace(content) && 
               content.Contains(triggerContent, StringComparison.OrdinalIgnoreCase);
    }

    public FileTypeResult Apply(MatchesResult content)
    {
        return result;
    }
}