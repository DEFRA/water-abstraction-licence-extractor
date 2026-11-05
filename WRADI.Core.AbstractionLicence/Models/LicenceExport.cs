namespace WRADI.Core.AbstractionLicence.Models;

/// Serialise with LicenceExportSerializer.Options, or the inherited NoneSchemaData goes too.
public class LicenceExport : Licence
{
    public ValueWithConfidence<string>? IssuedTo { get; init; }

    public DocumentTextPage[]? DocumentText { get; init; }

    public LicenceVerification[] Verifications { get; init; } = [];
}
