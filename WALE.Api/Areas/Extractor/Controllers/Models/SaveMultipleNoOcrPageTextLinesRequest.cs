namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveMultipleNoOcrPageTextLinesRequest
{
    public List<SaveNoOcrPageTextLinesRequest> pages { get; set; } = [];
}
