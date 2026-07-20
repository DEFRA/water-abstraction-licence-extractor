namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class CreateLicenceSet
{
    public required string LicenceSetId { get; init; }

    public string? ShortLicenceSetId { get; init; }

    public int? LicenceSetType { get; init; }

    public int[] LicenceSetTypes { get; init; } = [];
}