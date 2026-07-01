namespace WALE.Api.Areas.BFF.Models;

public class AwsQueueConfig
{
    public string? OrchestratorQueue { get; set; }

    public string? FileProcessQueue { get; set; }
}