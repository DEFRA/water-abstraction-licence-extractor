namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveLicenceSetRequest
{
    public string? licenceSet { get; set; }
        
    public Guid fileId { get; set; }
        
    public int processRunId { get; set; }
}