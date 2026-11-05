namespace WALE.ProcessFile.Core.Models.OcrService;

public class OcrServiceFailure
{
    public string ServiceName { get; set; } = string.Empty;

    public int PageNumber { get; set; }

    public int ImageNumber { get; set; }
    
    public bool IsTransient { get; set; }
    
    public string ErrorType { get; set; } = string.Empty;

    public string? Message { get; set; }
}
