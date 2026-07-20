namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class LicenceListLicenceSetRow
{
    public required string LicenceSetId { get; set; }

    public string? ShortLicenceSetId { get; set; }

    public int? LicenceSetType { get; set; }

    public int[] LicenceSetTypes { get; set; } = [];
}