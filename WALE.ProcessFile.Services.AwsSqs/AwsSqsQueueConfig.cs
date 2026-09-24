namespace WALE.ProcessFile.Services.AwsSqs;

// One shared queue pair for every document type - the consumer picks the right
// document-type-specific implementation per message (see DocumentType on
// FileProcessOrchestrationRequest/FileProcessSingleRequest), not per queue/deployment.
public class AwsSqsQueueConfig
{
    public string? OrchestratorQueue { get; set; }

    public string? FileProcessQueue { get; set; }
}