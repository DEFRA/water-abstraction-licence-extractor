namespace WALE.ProcessFile.Core.Models;

public record NaldLicenceSimple
{
    public required string LicenceNumber { get; init; }
    public required short RegionCode { get; init; }
}