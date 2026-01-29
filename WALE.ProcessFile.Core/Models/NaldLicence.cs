using WALE.ProcessFile.Core.Enums;

namespace WALE.ProcessFile.Core.Models;

public record NaldLicence
{
    public required string LicenceNumber { get; init; }
    public required string RegionCode { get; init; }
    public required int Id { get; init; }
    public required LicenceType Type { get; init; }
}