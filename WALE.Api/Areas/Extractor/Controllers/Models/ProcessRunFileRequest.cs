namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class ProcessRunFileRequest
{
    public string? FileName { get; set; }

    public int ProcessRunId { get; set; }
    public int ProcessRunFileId { get; set; }

    public string? ErrorMessage { get; set; }
}