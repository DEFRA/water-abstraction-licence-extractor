namespace WALE.ProcessFile.Core.Models;

public class FileProcessOrchestrationRequest
{
    public string? DocumentType { get; set; }

    public DateTime RequestedAt { get; set; }
    
    public int? RegionId { get; set; }
    
    public int MaxLicencesToTake { get; set; }
}