using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class SaveMatchesRequest
{
    public List<MatchToSave>? matches { get; set; }
}