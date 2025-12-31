using WALE.ProcessFile.Core.Enums.OutputSchema;

namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LicenceSetReference
{
    public string? LicenceSetId { get; init; }
    
    public LicenceSetType LicenceSetType { get; init; }
}