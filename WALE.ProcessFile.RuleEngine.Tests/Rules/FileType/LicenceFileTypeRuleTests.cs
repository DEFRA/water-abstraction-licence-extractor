using FluentAssertions;
using WALE.ProcessFile.RuleEngine.Rules.FileType;
using Xunit;

namespace WALE.ProcessFile.RuleEngine.Tests.Rules.FileType;

public class LicenceFileTypeRuleTests
{
    private readonly LicenceFileTypeRule _rule;

    public LicenceFileTypeRuleTests()
    {
        _rule = new LicenceFileTypeRule();
    }

    [Fact]
    public void RuleName_ShouldReturnCorrectName()
    {
        // Assert
        _rule.RuleName.Should().Be("LicenceFileType");
    }

    [Fact]
    public void Priority_ShouldReturn95()
    {
        // Assert
        _rule.Priority.Should().Be(100);
    }

    [Fact]
    public void Apply_ShouldReturnLicenseFileType()
    {
        // Arrange
        var content = "SCHEDULE OF CONDITIONS";

        // Act
        var result = _rule.Apply(content);

        // Assert
        result.Should().NotBeNull();
        result.FileType.Should().Be("Licence");
        result.IdentifiedByRule.Should().Be("LicenceFileType");
        result.MatchedTerms.Should().Contain("SCHEDULE OF CONDITIONS");
    }
}
