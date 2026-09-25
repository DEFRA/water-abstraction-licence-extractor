namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class UpdateThumbnailPathRequest
{
    public Guid fileId  { get; set; }
    
    public string? url { get; set; }
}