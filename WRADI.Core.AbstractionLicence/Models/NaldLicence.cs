using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public record NaldLicence : NaldLicenceSimple
{
    public required int Id { get; init; }
    public required LicenceType Type { get; init; }
}