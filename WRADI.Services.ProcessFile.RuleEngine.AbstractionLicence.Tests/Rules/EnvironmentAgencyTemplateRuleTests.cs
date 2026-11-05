using FluentAssertions;
using WALE.ProcessFile.Core.Models;
using WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Rules;
using Xunit;

namespace WRADI.Services.ProcessFile.RuleEngine.AbstractionLicence.Tests.Rules;

public class EnvironmentAgencyTemplateRuleTests
{
    private readonly EaDigitalTemplateConfigurationRule _digital = new();
    private readonly EaScannedWithoutSplitTemplateConfigurationRule _scanned = new() { Region = "NE" };
    private readonly EaScannedWithSplitTemplateConfigurationRule _scannedWithSplit = new() { Region = "NE" };

    private static MatchesResult Ea(bool scanned, bool withSplitLabels = false)
    {
        var matches = new List<LabelGroupResult> { new() { LabelGroupName = "EALabel" } };

        if (withSplitLabels)
        {
            matches.Add(new LabelGroupResult { LabelGroupName = "NESplitLabels" });
        }

        return new MatchesResult { ScannedFile = scanned, Matches = matches };
    }

    [Fact]
    public void ANativeEaDocumentIsNotClaimedByEitherScannedRule()
    {
        _scanned.CanApply(Ea(scanned: false)).Should().BeFalse();
        _scannedWithSplit.CanApply(Ea(scanned: false, withSplitLabels: true)).Should().BeFalse();
        _digital.CanApply(Ea(scanned: false)).Should().BeTrue();
        _digital.Apply(Ea(scanned: false)).TemplateType.Should().Be("EA-Digital");
    }

    [Fact]
    public void AScannedEaDocumentStillClassifiesAsScanned()
    {
        _scanned.CanApply(Ea(scanned: true)).Should().BeTrue();
        _scanned.Apply(Ea(scanned: true)).TemplateType.Should().Be("EA-Scanned");
    }

    [Fact]
    public void AScannedEaDocumentCarryingAuthorityHeadersKeepsTheSplitSignal()
    {
        var content = Ea(scanned: true, withSplitLabels: true);

        _scannedWithSplit.CanApply(content).Should().BeTrue();
        _scannedWithSplit.Apply(content).TemplateType.Should().Contain("Split Required");
        _scanned.CanApply(content).Should().BeFalse();
    }

    [Fact]
    public void TheScannedRulesOutrankEaDigital()
    {
        _scanned.Priority.Should().BeGreaterThan(_digital.Priority);
        _scannedWithSplit.Priority.Should().BeGreaterThan(_digital.Priority);
    }
}
