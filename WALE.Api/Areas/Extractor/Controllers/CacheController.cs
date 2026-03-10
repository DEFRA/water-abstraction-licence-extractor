using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class CacheController(ICacheService cacheService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> ClearAllAsync()
    {
        await cacheService.ClearCacheAsync();
        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> ClearSingleAsync(
        [FromQuery] string pdfFilename)
    {
        await cacheService.ClearCacheAsync(pdfFilename);
        return Ok();
    }
}