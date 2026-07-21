namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class UpdateLicenceRequest
{
    public string? licence  { get; set; }
    
    public int licenceId { get; set; }
    
    public int processRunId { get; set; }
}