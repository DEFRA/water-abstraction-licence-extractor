using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class DmsController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetFileIdsAsync()
    {
        var dmsFileIdInformation =
            await cacheService.GetDmsFileIdInformationAsync();
        
        return Ok(dmsFileIdInformation);
    }
}