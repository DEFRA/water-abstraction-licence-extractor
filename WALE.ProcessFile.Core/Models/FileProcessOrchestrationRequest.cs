namespace WALE.ProcessFile.Core.Models;

// The orchestrator queue's message body. Previously an inline anonymous object carrying just
// RequestedAt (and never actually deserialized by either consuming Lambda) - now the queue is
// shared across all document types, so the consumer needs DocumentType to pick which one's
// IFileProcessOrchestrator to run.
public class FileProcessOrchestrationRequest
{
    // Defaults to AbstractionLicence for backward compatibility with anything already in
    // flight/unaware of this field.
    public string DocumentType { get; set; } = "AbstractionLicence";

    public DateTime RequestedAt { get; set; }
}
