namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveStubMatchResultRequest
{
    public string? filename { get; set; }
    
    public Guid fileId { get; set; }
    
    public int processRunId { get; set; }
}