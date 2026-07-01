namespace WALE.Api.Areas.BFF.Models;

public class SendFileProcessSingleMessageRequest
{
    public string? FilePath { get; set; }
    
    public string? ProcessRunId { get; set; }
}