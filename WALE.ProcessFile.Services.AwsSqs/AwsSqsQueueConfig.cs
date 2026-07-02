namespace WALE.ProcessFile.Services.AwsSqs;

public class AwsQueueConfig
{
    public string? OrchestratorQueue { get; set; }

    public string? FileProcessQueue { get; set; }
}