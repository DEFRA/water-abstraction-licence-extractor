namespace WALE.ProcessFile.Core.Models;

public class NoOcrServicePageCacheRequest
{
    public string? Filename { get; set; }
    public int PageNumber { get; set; }
    public string? NoOcrServiceName  { get; set; }
    public int ProcessRunId { get; set; }
}