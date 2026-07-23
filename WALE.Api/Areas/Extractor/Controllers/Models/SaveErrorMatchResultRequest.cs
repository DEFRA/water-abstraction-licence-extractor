namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveErrorMatchResultRequest
{
    public string? filename { get; set; }
    
    public Guid fileId { get; set; }
    
    public int processRunId { get; set; }
    
    public string? error { get; set; }
    
    public bool isUpdate { get; set; }
}