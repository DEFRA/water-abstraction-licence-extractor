namespace WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemVerificationItem
{
    public long VerificationItemId { get; set; }

    public long VerificationSectionId { get; set; }

    public required string LicenceSectionItemId { get; set; }

    public List<LicenceListItemVerificationType> VerificationTypes { get; init; } = [];
}