namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveLicenceSetsRequest
{
    public string? licenceSets { get; set; }
        
    public Guid fileId { get; set; }
        
    public int processRunId { get; set; }
}