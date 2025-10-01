using WALE.ProcessFile.Services.Enums.OutputSchema;

namespace WALE.ProcessFile.Services.Models.OutputSchema;

public class LicenceSetReference
{
    public string? LicenceSetId { get; init; }
    
    public LicenceSetType LicenceSetType { get; init; }
}