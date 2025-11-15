using FluentAssertions;
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

    [Fact]
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
    }

    [Fact]
    public void Evaluate_WithApplicableRule_ShouldReturnResult()
    {
        // Arrange
        var ruleEngine = new RuleEngine<FileTypeResult>();
        var expectedResult = new FileTypeResult { FileType = "Schedule" };
        var rule = new TestRule("ScheduleRule", 100, "change in", expectedResult);
        ruleEngine.AddRule(rule);

        // Act
        var result = ruleEngine.Evaluate("This document contains change in conditions");

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

        // Act
        var result = ruleEngine.Evaluate("This document contains no matching terms");

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

        // Act
        var results = ruleEngine.EvaluateAll("This is a test document").ToList();

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

        // Act
        var result = ruleEngine.Evaluate("This is a test document");

        // Assert
        result.Should().NotBeNull();
        result!.FileType.Should().Be("High");
    }
}

// Test helper class
public class TestRule : IRule<FileTypeResult>
{
    private readonly string _triggerContent;
    private readonly FileTypeResult _result;

    public TestRule(string ruleName, int priority, string triggerContent, FileTypeResult result)
    {
        RuleName = ruleName;
        Priority = priority;
        _triggerContent = triggerContent;
        _result = result;
    }

    public string RuleName { get; }
    public int Priority { get; }

    public bool CanApply(string content)
    {
        return !string.IsNullOrWhiteSpace(content) && 
               content.Contains(_triggerContent, StringComparison.OrdinalIgnoreCase);
    }

    public FileTypeResult Apply(string content)
    {
        return _result;
    }
}
