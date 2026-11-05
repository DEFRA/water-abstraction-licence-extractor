namespace WRADI.Core.AbstractionLicence.Models;

public class LicenceVerificationLookups
{
    public Dictionary<(Guid FileId, string LicenceNumber), List<LicenceSectionVerification>> BySourceLicence { get; set; } = new();
    
    public Dictionary<string, List<LicenceSectionVerification>> ByItemId { get; set; } = new();
}