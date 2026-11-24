namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class ProcessRunNameRequest
{
    public int processRunId { get; set; }

    public string? name { get; set; }
}
