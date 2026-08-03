namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class UpsertLicenceSetItem
{
    public string? LicenceSetId { get; init; }

    public string? ShortLicenceSetId { get; init; }

    public string? LicenceSetType { get; init; }

    public string[] LicenceSetTypes { get; init; } = [];
}