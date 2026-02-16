using FluentAssertions;
using WALE.ProcessFile.RuleEngine.Models;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Models;

public class FileTypeResultTests
{
    [Fact]
    public void Constructor_ShouldInitializeProperties()
    {
        // Act
        var result = new FileTypeResult();

        // Assert
        result.FileType.Should().Be(string.Empty);
        result.Confidence.Should().Be(0.0);
        result.IdentifiedByRule.Should().Be(string.Empty);
        result.Metadata.Should().NotBeNull();
        result.Metadata.Should().BeEmpty();
        result.MatchedTerms.Should().NotBeNull();
        result.MatchedTerms.Should().BeEmpty();
    }

    [Fact]
    public void Properties_ShouldBeSettable()
    {
        // Arrange
        var result = new FileTypeResult();
        var expectedMetadata = new Dictionary<string, object> { { "key", "value" } };
        var expectedMatchedTerms = new List<string> { "term1", "term2" };

        // Act
        result.FileType = "Schedule";
        result.Confidence = 0.95;
        result.IdentifiedByRule = "ScheduleRule";
        result.Metadata = expectedMetadata;
        result.MatchedTerms = expectedMatchedTerms;

        // Assert
        result.FileType.Should().Be("Schedule");
        result.Confidence.Should().Be(0.95);
        result.IdentifiedByRule.Should().Be("ScheduleRule");
        result.Metadata.Should().BeEquivalentTo(expectedMetadata);
        result.MatchedTerms.Should().BeEquivalentTo(expectedMatchedTerms);
    }

    [Fact]
    public void Metadata_ShouldSupportDifferentValueTypes()
    {
        // Arrange
        var result = new FileTypeResult
        {
            Metadata =
            {
                // Act
                ["StringValue"] = "test",
                ["IntValue"] = 42,
                ["DoubleValue"] = 3.14,
                ["BoolValue"] = true
            }
        };

        // Assert
        result.Metadata["StringValue"].Should().Be("test");
        result.Metadata["IntValue"].Should().Be(42);
        result.Metadata["DoubleValue"].Should().Be(3.14);
        result.Metadata["BoolValue"].Should().Be(true);
    }

    [Fact]
    public void MatchedTerms_ShouldSupportMultipleTerms()
    {
        // Arrange
        var result = new FileTypeResult();
        var terms = new List<string> { "change in", "schedule", "conditions" };

        // Act
        result.MatchedTerms = terms;

        // Assert
        result.MatchedTerms.Should().HaveCount(3);
        result.MatchedTerms.Should().Contain("change in");
        result.MatchedTerms.Should().Contain("schedule");
        result.MatchedTerms.Should().Contain("conditions");
    }
}