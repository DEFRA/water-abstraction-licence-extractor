namespace WALE.ProcessFile.Models;

public class OcrServiceImageTextCacheRequest
{
    public string? Filepath { get; set; }
    public int PageNumber { get; set; }
    public int ImageNumber { get; set; }
    public string? OcrServiceName  { get; set; }
}