using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LicenceFinderController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetResultsAsync()
    {
        var date = await cacheService.GetLicenceFinderResultsAsync();
        return Ok(date);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveResultsAsync(
        [FromBody] LicenceFinderCreateRequest request)
    {
        await cacheService.SaveLicenceFinderResultsAsync(request.results!);
        return Ok();
    }
}