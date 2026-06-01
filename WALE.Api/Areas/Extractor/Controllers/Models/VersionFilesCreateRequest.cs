using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class VersionFilesCreateRequest
{
    public List<VersionFile>? results { get; set; }
}