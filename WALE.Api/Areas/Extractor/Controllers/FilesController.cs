using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class FilesController(IFileService fileService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetAsync([FromQuery] string filename)
    {
        var data = await fileService.GetFileAsStreamAsync(filename);
        return File(data, "application/pdf");
    }
}