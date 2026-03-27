namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveMatchRequest
{
    public int matchesResultId { get; set; }
    public string? labelName { get; set; }
    public string? labelGroupName { get; set; }
    public string? data { get; set; }
}