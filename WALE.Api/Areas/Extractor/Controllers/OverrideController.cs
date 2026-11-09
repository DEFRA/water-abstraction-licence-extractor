using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class OverrideController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync()
    {
        var overrides = await cacheService.GetOverridesAsync();
        return Ok(overrides);
    }
}
