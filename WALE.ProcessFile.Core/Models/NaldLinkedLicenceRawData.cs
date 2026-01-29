using WALE.ProcessFile.Core.Enums;

namespace WALE.ProcessFile.Core.Models;

public record NaldLinkedLicenceRawData
{
    public required string LicenceNumber { get; init; }
    public required string? Param1 { get; init; }
    public required string? Param2 { get; init; }
    public required string? Text { get; init; }
    public required string? Notes { get; init; }
    public required string RegionCode { get; init; }
    public required int Id { get; init; }

    public NaldLicence ToNaldLicence()
        => new NaldLicence
        {
            Id = Id,
            LicenceNumber = LicenceNumber,
            RegionCode = RegionCode,
            Type = LicenceType.Abstraction
        };
}
