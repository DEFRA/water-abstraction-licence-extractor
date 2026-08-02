using WRADI.Core.AbstractionLicence.Enums;

namespace WRADI.Core.AbstractionLicence.Models;

public class OutputListDataItemLicenceSet
{
    public string? LicenceSetId { get; set; }
    
    public string? ShortLicenceSetId { get; set; }
    
    public LicenceSetType LicenceSetType { get; init; }

    public LicenceSetType[] LicenceSetTypes { get; init; } = [];
}