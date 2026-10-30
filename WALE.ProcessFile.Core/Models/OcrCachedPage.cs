namespace WALE.ProcessFile.Core.Models;

public class OcrCachedPage
{
    public string OcrServiceName { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public int ImageNumber { get; set; }

    public string Data { get; set; } = string.Empty;
}
