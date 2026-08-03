namespace WRADI.Core.AbstractionLicence.Models;

public class LicenceVerificationLookups
{
    public Dictionary<Guid, List<LicenceSectionVerification>> ByFileId { get; set; } = new();
    
    public Dictionary<string, List<LicenceSectionVerification>> ByItemId { get; set; } = new();
}