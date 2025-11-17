using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models.OutputSchema;

public class LicenceSetReference
{
    public string? LicenceSetId { get; init; }
    
    public LicenceSetType LicenceSetType { get; init; }
}