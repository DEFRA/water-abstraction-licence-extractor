namespace WALE.ProcessFile.Core.Models;

public class FileProcessOrchestrationRequest
{
    public string DocumentType { get; set; } = "AbstractionLicence";

    public DateTime RequestedAt { get; set; }

    public int? RegionId { get; set; }

    // Null means no cap: a message serialised without this field must run unfiltered, not empty.
    public int? MaxLicencesToTake { get; set; }
}
