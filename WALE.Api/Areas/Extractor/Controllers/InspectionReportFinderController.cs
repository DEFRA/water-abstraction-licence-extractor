using Microsoft.AspNetCore.Mvc;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class InspectionReportFinderController(IInspectionReportFinderCacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetResultsAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var results = await cacheService.GetInspectionReportFinderResultsAsync(skip, take);
        return Ok(results);
    }

    [HttpPost]
    public async Task<ActionResult> SaveResultsAsync(
        [FromBody] List<InspectionReportFinderResult> results)
    {
        await cacheService.SaveInspectionReportFinderResultsAsync(results);
        return Ok();
    }

    [HttpPost]
    public async Task<ActionResult> ClearResultsAsync()
    {
        await cacheService.ClearInspectionReportFinderResultsAsync();
        return Ok();
    }
}
