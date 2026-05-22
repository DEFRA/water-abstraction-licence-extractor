using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers.Models;

public class VersionFilesToDownloadCreateRequest
{
    public List<VersionFileToDownload>? results { get; set; }
}