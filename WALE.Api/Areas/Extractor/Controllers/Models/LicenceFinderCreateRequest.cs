using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class LicenceFinderCreateRequest
{
    public List<LicenceFinderResult>? results { get; set; }
}