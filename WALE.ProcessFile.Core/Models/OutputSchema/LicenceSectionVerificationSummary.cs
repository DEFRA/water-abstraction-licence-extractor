namespace WALE.ProcessFile.Core.Models.OutputSchema;

public sealed record LicenceSectionVerificationSummary
{
    public required string LicenceSectionName { get; set; }

    public required LicenceSectionItemSummary[] LicenceSectionItems { get; set; }
}