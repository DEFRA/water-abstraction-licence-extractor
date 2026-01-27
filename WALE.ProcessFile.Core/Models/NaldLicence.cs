using WALE.ProcessFile.Core.Enums;

namespace WALE.ProcessFile.Core.Models;

public class NaldLicence
{
    public required string LicenceNumber { get; set; }
    public required string RegionCode { get; set; }
    public required int Id { get; set; }
    public required LicenceType Type { get; set; }
}
