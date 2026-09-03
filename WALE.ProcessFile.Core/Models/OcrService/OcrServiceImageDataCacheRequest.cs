namespace WALE.ProcessFile.Core.Models.OcrService;

public class OcrServiceImageDataCacheRequest
{
    public Guid FileId { get; set; }
    public int? PageNumber { get; set; }
    public int? ImageNumber { get; set; }
    public string? NoOcrServiceName  { get; set; }
    public string? Extension { get; set; }
    public int ProcessRunId { get; set; }
}