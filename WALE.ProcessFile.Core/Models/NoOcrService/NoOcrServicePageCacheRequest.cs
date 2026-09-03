namespace WALE.ProcessFile.Core.Models.NoOcrService;

public class NoOcrServicePageCacheRequest
{
    public Guid FileId { get; set; }
    public int PageNumber { get; set; }
    public string? NoOcrServiceName  { get; set; }
    public int ProcessRunId { get; set; }
}