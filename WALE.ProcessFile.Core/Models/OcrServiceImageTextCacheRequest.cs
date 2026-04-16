namespace WALE.ProcessFile.Core.Models;

public class OcrServiceImageTextCacheRequest
{
    public Guid FileId { get; set; }
    public int PageNumber { get; set; }
    public int ImageNumber { get; set; }
    public string? OcrServiceName  { get; set; }
    public int ProcessRunId { get; set; }
}