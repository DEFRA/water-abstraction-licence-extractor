namespace WALE.ProcessFile.Core.Models;

public class InvertedLicenceSectionVerification
{
    public required LicenceSectionVerification Verification { get; set; }
    public string? SourceLicenceNumber { get; set; }
}
