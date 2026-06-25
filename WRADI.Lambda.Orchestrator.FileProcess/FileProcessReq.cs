namespace WRADI.Lambda.Orchestrator.FileProcess;

public class FileProcessReq
{
    public string? FilePath { get; set; }

    public int? ProcessRunId { get; set; }

    public string? RequestedAt { get; set; }
}