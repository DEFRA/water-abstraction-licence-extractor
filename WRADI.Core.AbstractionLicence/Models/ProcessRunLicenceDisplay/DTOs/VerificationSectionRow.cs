namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay.DTOs;

public sealed class VerificationSectionRow
{
    public long LicenceListItemId { get; init; }

    public long VerificationSectionId { get; init; }

    public string LicenceSectionName { get; init; } =
        string.Empty;

    public long? VerificationItemId { get; init; }

    public string? LicenceSectionItemId { get; init; }

    public bool ScrapedDataIsDifferent { get; init; }

    public string[] VerificationTypes { get; init; } = [];
}