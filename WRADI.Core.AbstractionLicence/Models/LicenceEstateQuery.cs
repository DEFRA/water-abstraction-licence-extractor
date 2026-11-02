using WALE.ProcessFile.Core.Models;

namespace WRADI.Core.AbstractionLicence.Models;

public sealed class LicenceEstateQuery : ProcessRunQuery
{
    /// Live, Dead, Impound or Not Found - JsOutputHelper's collapse of NaldStatus and
    /// LicenceType, not the raw NALD status.
    public string? ProjectedStatus { get; init; }

    public int? ProcessRunId { get; init; }
}
