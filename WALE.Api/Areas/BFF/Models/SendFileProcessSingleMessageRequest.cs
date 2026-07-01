namespace WALE.Api.Areas.BFF.Models;

public class SendFileProcessSingleMessageRequest
{
    public string? filePath { get; set; }
    
    public string? processRunId { get; set; }
}