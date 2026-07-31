namespace WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

public sealed class LicenceListItemVerificationSection
{
    public long VerificationSectionId { get; set; }

    public int ProcessRunId { get; set; }

    public long LicenceListItemId { get; set; }

    public required string LicenceSectionName { get; set; }

    public List<LicenceListItemVerificationItem> LicenceSectionItems { get; init; } = [];
}