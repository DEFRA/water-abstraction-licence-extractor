using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class ImportController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetDateAsync(string dataSource)
    {
        var date = await cacheService.GetImportRunDateAsync(dataSource);
        return Ok(date);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveDateAsync(
        [FromBody] SaveDateRequest request)
    {
        await cacheService.SaveImportRunDateAsync(request.dataSource!);
        return Ok();
    }
}