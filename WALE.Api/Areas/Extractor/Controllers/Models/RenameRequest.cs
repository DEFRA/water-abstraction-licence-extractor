namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class RenameRequest
{
    public string? originalFilename{ get; set; }
    public string? newFilename{ get; set; }
}