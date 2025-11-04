using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models;

public class OutputListDataItemLicenceSet
{
    public string? LicenceSetId { get; set; }
    
    public string? ShortLicenceSetId { get; set; }
    
    public LicenceSetType LicenceSetType { get; init; }

    public LicenceSetType[] LicenceSetTypes { get; init; } = [];
}