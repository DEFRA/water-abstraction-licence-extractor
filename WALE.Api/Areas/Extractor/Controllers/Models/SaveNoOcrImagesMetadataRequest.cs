namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveNoOcrImagesMetadataRequest
{
    public Guid fileId { get; set; }
    public string? imagesMetadata { get; set; }
    public int processRunId { get; set; }
    public string? noOcrServiceName  { get; set; }
}