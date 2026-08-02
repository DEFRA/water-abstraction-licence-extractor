namespace WRADI.Core.AbstractionLicence.Models;

public record NaldLicenceSimple
{
    public required string LicenceNumber { get; init; }
    public required short RegionCode { get; init; }
}