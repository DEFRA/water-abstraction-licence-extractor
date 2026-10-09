using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.BFF.Models;

public class LicenceSectionVerificationOutput: LicenceSectionVerification
{
    public string? SourceLicenceNumber { get; set; }
}