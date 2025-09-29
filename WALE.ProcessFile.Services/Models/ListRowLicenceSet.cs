using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models;

public class ListRowLicenceSet
{
    public string? LicenceSetId { get; set; }
    
    public string? ShortLicenceSetId { get; set; }
    
    public LicenceSetType LicenceSetType { get; init; }
}