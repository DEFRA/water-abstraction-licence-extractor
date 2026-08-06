using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public record NaldLinkedLicenceRawData
{
    public required string LicenceNumber { get; init; }
    public required string? Param1 { get; init; }
    public required string? Param2 { get; init; }
    public required string? AcinCode { get; init; }
    public required string? Text { get; init; }
    public required string? Notes { get; init; }
    public required short RegionCode { get; init; }
    public required int Id { get; init; }

    public NaldLicence ToNaldLicence()
        => new()
        {
            Id = Id,
            LicenceNumber = LicenceNumber,
            RegionCode = RegionCode,
            Type = LicenceType.Abstraction
        };
}