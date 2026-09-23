namespace WALE.ProcessFile.Core.Models;

public class FileProcessOrchestrationRequest
{
    public string DocumentType { get; set; } = "AbstractionLicence";

    public DateTime RequestedAt { get; set; }
}
