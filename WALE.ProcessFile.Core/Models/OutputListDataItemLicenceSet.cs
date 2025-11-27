using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models;

public class OutputListDataItemLicenceSet
{
    public string? LicenceSetId { get; set; }
    
    public string? ShortLicenceSetId { get; set; }
    
    public LicenceSetType LicenceSetType { get; init; }

    public LicenceSetType[] LicenceSetTypes { get; init; } = [];
}