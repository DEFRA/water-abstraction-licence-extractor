namespace WALE.ProcessFile.Core.Models.OcrService;

public class OcrServiceAllTextCacheRequest
{
    public Guid FileId { get; set; }
    public string? OcrServiceName { get; set; }

    // GetAllOcrTextAsync's SQL builds these same keys - the two must agree.
    public static string ImageKey(int pageNumber, int imageNumber) => $"i:{pageNumber}:{imageNumber}";

    public static string ScreenshotKey(int pageNumber) => $"s:{pageNumber}";
}
