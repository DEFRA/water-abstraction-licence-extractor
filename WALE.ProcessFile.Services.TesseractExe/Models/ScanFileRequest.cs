namespace WALE.ProcessFile.Services.TesseractExe.Models;

public class ScanFileRequest
{
    public int PageNumber { get; set; }
    
    public int ImageNumber { get; set; }
    
    public string? ImageReference { get; set; }
    
    public Guid FileId { get; set; }
    
    public bool IsPageScreenshot { get; set; }
    
    public int ProcessRunId { get; set; }
}