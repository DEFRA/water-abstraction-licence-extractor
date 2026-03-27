namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveNoOcrPageTextLinesRequest
{
    public Guid fileId { get; set; }
    public int pageNumber { get; set; }
    public string? noOcrServiceName  { get; set; }
    public int processRunId { get; set; }
    public string? pageLines  { get; set; }
}