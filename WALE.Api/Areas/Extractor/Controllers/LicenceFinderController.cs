using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WRADI.Core.AbstractionLicence.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LicenceFinderController(
    IAbstractionLicenceCacheService abstractionLicenceCacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetAsync([FromQuery] Guid fileId)
    {
        var date = await abstractionLicenceCacheService.GetLicenceFinderResultAsync(fileId);
        return Ok(date);
    }
    
    [HttpGet]
    public async Task<ActionResult> GetResultsAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var date = await abstractionLicenceCacheService.GetLicenceFinderResultsAsync(skip, take);
        return Ok(date);
    }
    
    [HttpPost]
    public async Task<ActionResult> ClearResultsAsync()
    {
        await abstractionLicenceCacheService.ClearLicenceFinderResultsAsync();
        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveResultsAsync(
        [FromBody] LicenceFinderCreateRequest request)
    {
        await abstractionLicenceCacheService.SaveLicenceFinderResultsAsync(request.results!);
        return Ok();
    }
}