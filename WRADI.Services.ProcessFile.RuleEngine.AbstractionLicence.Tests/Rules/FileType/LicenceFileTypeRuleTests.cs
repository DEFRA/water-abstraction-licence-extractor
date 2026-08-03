using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules.FileType;
using Xunit;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Tests.Rules.FileType;

public class LicenceFileTypeRuleTests
{
    private readonly LicenceFileTypeRule _rule = new();

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

        var documentLine = new DocumentLine(
            0,
            0,
            [new DocumentLineColumn(DocumentLineColumn.TextToWords(content, null))],
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
                    LabelGroupName = "Licence Header"
                }
            ]
        };

        // Act
        var result = _rule.Apply(matchesResult);

        // Assert
        result.Should().NotBeNull();
        result.FileType.Should().Be("Licence");
        result.IdentifiedByRule.Should().Be("LicenceFileType");
        result.MatchedTerms.Should().Contain("SCHEDULE OF CONDITIONS");
    }
}
