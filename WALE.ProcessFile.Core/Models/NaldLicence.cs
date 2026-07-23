using WALE.ProcessFile.Core.Enums;

namespace WALE.ProcessFile.Core.Models;

public record NaldLicence : NaldLicenceSimple
{
    public required int Id { get; init; }
    public required LicenceType Type { get; init; }
}