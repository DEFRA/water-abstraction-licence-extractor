namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class GeneratePresignedUrlsRequest
{
    public List<Guid>? fileIds { get; set; }
    
    public string? templateUrl { get; set; }
}