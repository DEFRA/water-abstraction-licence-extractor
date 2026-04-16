namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveLicenceRequest
{
    public string? licence  { get; set; }
    
    public int processRunId { get; set; }
}