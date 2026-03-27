namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveOcrImageTextRequest
{
    public Guid fileId { get; set; }
    public int pageNumber { get; set; }
    public int imageNumber { get; set; }
    public string? ocrServiceName  { get; set; }
    public int processRunId { get; set; }
    public string? pageLines { get; set; }
}