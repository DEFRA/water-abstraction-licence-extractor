namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveImageOnPageRequest
{
    public Guid fileId { get; set; }
    
    public byte[] bytes { get; set; } = [];

    public int width { get; set; }

    public int height { get; set; }

    public string? noOcrServiceName { get; set; }

    public int imageNumber { get; set; }

    public int pageNumber { get; set; }

    public string? extension { get; set; }

    public int processRunId { get; set; }
}