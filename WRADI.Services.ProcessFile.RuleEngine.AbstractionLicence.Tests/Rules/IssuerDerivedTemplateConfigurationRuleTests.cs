using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;
using Xunit;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Tests.Rules;

public class IssuerDerivedTemplateConfigurationRuleTests
{
    private readonly IssuerDerivedTemplateConfigurationRule _rule = new();

    private static MatchesResult WithIssuer(params string[] lines)
    {
        return new MatchesResult
        {
            Matches =
            [
                new LabelGroupResult
                {
                    LabelGroupName = "Issuer",
                    Text = lines.Select(l => new DocumentLine(
                        0, 0, [new DocumentLineColumn(DocumentLineColumn.TextToWords(l, null))], 0, 0, 0, 0)).ToList()
                }
            ]
        };
    }

    /// The eight issuer strings present across the 2,137-licence estate on dev, 2026-10-29.
    [Theory]
    [InlineData("ENVIRONMENT AGENCY", "EA-Scanned")]
    [InlineData("NATIONAL RIVERS AUTHORITY", "NRA-Scanned")]
    [InlineData("YORKSHIRE WATER AUTHORITY", "YORKSHIRE WATER AUTHORITY")]
    [InlineData("YORKSHIRE OUSE AND HULL RIVER AUTHORITY", "YORKSHIRE OUSE AND HULL RIVER AUTHORITY")]
    [InlineData("NORTHUMBRIAN RIVER AUTHORITY", "NORTHUMBRIAN RIVER AUTHORITY")]
    [InlineData("YORKSHIRE RIVER AUTHORITY", "YORKSHIRE RIVER AUTHORITY")]
    [InlineData("NORTHUMBRIAN WATER AUTHORITY", "NORTHUMBRIAN WATER AUTHORITY")]
    [InlineData("SUSSEX RIVER AUTHORITY", "SUSSEX RIVER AUTHORITY")]
    public void Classifies_EveryIssuerStringPresentInTheEstate(string issuer, string expectedTemplateType)
    {
        var content = WithIssuer(issuer);

        _rule.CanApply(content).Should().BeTrue();
        _rule.Apply(content).TemplateType.Should().Be(expectedTemplateType);
    }

    [Fact]
    public void NationalRivers_IsNotClaimedByTheGenericRiverAuthorityTest()
    {
        _rule.Apply(WithIssuer("NATIONAL RIVERS AUTHORITY")).TemplateType.Should().Be("NRA-Scanned");
    }

    [Theory]
    [InlineData("Yorkshire  Water   Authority")]
    [InlineData("yorkshire water authority")]
    [InlineData(" YORKSHIRE WATER AUTHORITY ")]
    public void Divisional_NormalisesCasingAndSpacingToOneValue(string issuer)
    {
        _rule.Apply(WithIssuer(issuer)).TemplateType.Should().Be("YORKSHIRE WATER AUTHORITY");
    }

    [Fact]
    public void Priority_IsLowerThanEveryHeaderRule()
    {
        _rule.Priority.Should().BeLessThan(new EaDigitalTemplateConfigurationRule().Priority);
        _rule.Priority.Should().BeLessThan(new DivisionalTemplateConfigurationRule().Priority);
    }

    [Fact]
    public void DoesNotClassify_WhenTheIssuerGroupIsAbsentOrUnrecognised()
    {
        _rule.CanApply(new MatchesResult { Matches = [] }).Should().BeFalse();
        _rule.CanApply(new MatchesResult()).Should().BeFalse();
        _rule.CanApply(WithIssuer("Some Water Company Limited")).Should().BeFalse();
    }
}
