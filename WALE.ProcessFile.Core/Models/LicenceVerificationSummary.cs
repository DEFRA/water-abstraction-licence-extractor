namespace WALE.ProcessFile.Core.Models;

public class LicenceVerificationSummary
{
    public Guid LicenceFileId { get; set; }
    public string? LicenceSectionName { get; set; }
    public string? VerificationType { get; set; }
}