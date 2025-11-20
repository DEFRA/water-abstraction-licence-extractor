using WALE.ProcessFile.Models.Enums.OutputSchema;

namespace WALE.ProcessFile.Models.OutputSchema.Table;

public class LicenceSetTable
{
    public int LicenceSetId { get; init; }

    public string? SchemaLicenceSetId { get; init; }   
    
    public string? ShortLicenceSetId { get; init; }
}