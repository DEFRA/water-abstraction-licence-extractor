namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SavePageScreenshotRequest
{
    public Guid fileId { get; set; }
    public int pageNumber { get; set; }
    public string? noOcrServiceName { get; set; }
    public byte[] data { get; set; } = [];
    public int processRunId { get; set; }
}