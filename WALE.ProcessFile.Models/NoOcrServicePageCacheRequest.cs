namespace WALE.ProcessFile.Models;

public class NoOcrServicePageCacheRequest
{
    public string? Filepath { get; set; }
    public int PageNumber { get; set; }
    public string? NoOcrServiceName  { get; set; }
}