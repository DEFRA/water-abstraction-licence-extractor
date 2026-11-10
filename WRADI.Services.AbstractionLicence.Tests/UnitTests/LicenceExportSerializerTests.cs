using System.Text.Json;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.AbstractionLicence.Tests.UnitTests;

public class LicenceExportSerializerTests
{
    private static LicenceExport Export() => new()
    {
        LicenceNumber = new ValueWithConfidence<string>("03/28/60/0130", 0.9, 0.8),
        NoneSchemaData = { ["licenceId"] = 4321, ["issuedTo"] = "a water company" }
    };

    [Fact]
    public void TheInheritedNoneSchemaDataIsNotWritten()
    {
        var json = JsonSerializer.Serialize(Export(), LicenceExportSerializer.Options);

        Assert.DoesNotContain("noneSchemaData", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("4321", json, StringComparison.Ordinal);
    }

    /// The strip matches the property's camelCase name, so dropping that policy would leak the
    /// blob back into every exported licence.
    [Fact]
    public void TheLicencesOwnFieldsAreWrittenInCamelCase()
    {
        var json = JsonSerializer.Serialize(Export(), LicenceExportSerializer.Options);

        Assert.Contains("\"licenceNumber\"", json, StringComparison.Ordinal);
        Assert.Contains("03/28/60/0130", json, StringComparison.Ordinal);
    }
}
