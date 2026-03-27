namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveTemporaryOcrImageTextRequest
{
    public Guid fileId { get; set; }
        
    public int processRunId { get; set; }
        
    public int pageNumber { get; set; }
        
    public int imageNumber { get; set; }
        
    public string? ocrServiceName { get; set; }
        
    public string? text { get; set; }
}