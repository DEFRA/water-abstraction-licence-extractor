namespace WALE.ProcessFile.Services.AwsSqs;

public class AwsSqsQueueConfig
{
    public string? OrchestratorQueue { get; set; }

    public string? FileProcessQueue { get; set; }
}