using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveMatchesRequest
{
    public List<(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)>? matches { get; set; }
}