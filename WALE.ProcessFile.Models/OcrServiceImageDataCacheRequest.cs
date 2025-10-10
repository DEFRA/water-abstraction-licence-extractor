namespace WALE.ProcessFile.Models;

public class OcrServiceImageDataCacheRequest
{
    public string? Filepath { get; set; }
    public int PageNumber { get; set; }
    public int ImageNumber { get; set; }
    public string? NoOcrServiceName  { get; set; }
    public string? Extension { get; set; }
}