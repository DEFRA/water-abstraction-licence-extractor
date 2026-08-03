namespace WRADI.Core.AbstractionLicence.Models;

public class LicenceSectionVerification
{
    public int LicenceSectionVerificationId { get; set; }
    public Guid LicenceFileId { get; set; }
    public int ProcessRunId { get; set; }
    public string? LicenceSectionName { get; set; }
    public string? LicenceSectionScrapedValue { get; set; }
    public string? LicenceSectionSnapshotValue { get; set; }
    public string? LicenceSectionOverrideValue { get; set; }
    public string? VerificationType { get; set; }
    public string? LicenceSectionItemId { get; set; }
    public string? Notes { get; set; }
    public bool ScrapedDataIsDifferent { get; set; }
    public DateTime CreatedDateTimeUtc { get; set; }
}

public record LicenceSectionVerificationSummary
{
    public required string LicenceSectionName { get; set; }
    public required LicenceSectionItemSummary[] LicenceSectionItems { get; set; }
}

public record LicenceSectionItemSummary
{
    public required string LicenceSectionItemId { get; set; }
    public required string[] VerificationTypes { get; set; }
    public bool ScrapedDataIsDifferent { get; set; }
}