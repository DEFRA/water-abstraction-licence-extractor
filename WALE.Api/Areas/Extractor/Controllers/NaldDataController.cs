using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class NaldDataController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] short regionCode)
    {
        var naldData = await cacheService.GetNaldDataAsync(regionCode);
        return Ok(naldData);
    }
}