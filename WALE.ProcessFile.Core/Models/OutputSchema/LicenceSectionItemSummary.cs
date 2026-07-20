namespace WALE.ProcessFile.Core.Models.OutputSchema;

public sealed record LicenceSectionItemSummary
{
    public required string LicenceSectionItemId { get; set; }

    public required string[] VerificationTypes { get; set; }
}