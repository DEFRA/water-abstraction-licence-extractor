namespace WALE.Api.Areas.BFF.Models;

public class SendFileProcessSingleMessageRequest
{
    public string? FilePath { get; set; }
    
    public int? ProcessRunId { get; set; }
}