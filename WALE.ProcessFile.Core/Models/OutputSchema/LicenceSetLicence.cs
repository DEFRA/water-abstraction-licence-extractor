namespace WALE.ProcessFile.Core.Models.OutputSchema;

public class LicenceSetLicence
{
    public int? LicenceId { get; set; }
    public string? LicenceNumber { get; init; }
    
    public int? RegionId { get; set; }
    
    public string? LicenceVersionId { get; init; }
    public int LicenceSetId { get; set; }
    public int ProcessRunId { get; set; }
}