namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveAllPagesTextRequest
{
    public Guid fileId { get; set; }
    public string? documentLines{ get; set; }
    public string? noOcrServiceName{ get; set; }
    public int processRunId{ get; set; }
}