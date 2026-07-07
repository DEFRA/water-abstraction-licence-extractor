namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class ProcessRunCreateRequest
{
    public string? description { get; set; }
    public int numberOfFiles { get; set; }
    public string? status { get; set; }
}