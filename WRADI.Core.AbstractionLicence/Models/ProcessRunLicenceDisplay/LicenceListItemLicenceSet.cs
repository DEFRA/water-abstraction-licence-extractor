using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemLicenceSet
{
    public long LicenceListItemLicenceSetId { get; set; }

    public int ProcessRunId { get; set; }

    public long LicenceListItemId { get; set; }

    public required string LicenceSetId { get; set; }

    public string? ShortLicenceSetId { get; set; }

    public LicenceSetType LicenceSetType { get; set; }

    public List<LicenceListItemLicenceSetType> LicenceSetTypes { get; init; } = [];
}