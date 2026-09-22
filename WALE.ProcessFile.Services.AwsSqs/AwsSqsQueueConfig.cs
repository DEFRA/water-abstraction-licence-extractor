namespace WALE.ProcessFile.Services.AwsSqs;

public class AwsSqsQueueConfig
{
    public string? OrchestratorQueue { get; set; }

    public string? FileProcessQueue { get; set; }

    public string? WrInspectionReportOrchestratorQueue { get; set; }

    public string? WrInspectionReportFileProcessQueue { get; set; }
}