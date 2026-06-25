using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class VersionFilesController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetToDownloadAsync()
    {
        var date = await cacheService.GetVersionFilesToDownloadAsync();
        return Ok(date);
    }
    
    [HttpGet]
    public async Task<ActionResult> GetAllAsync()
    {
        var date = await cacheService.GetVersionFilesAsync();
        return Ok(date);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveToDownloadAsync(
        [FromBody] VersionFilesToDownloadCreateRequest request)
    {
        await cacheService.SaveVersionFilesToDownloadAsync(request.results!);
        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveAllAsync(
        [FromBody] VersionFilesCreateRequest request)
    {
        await cacheService.SaveVersionFilesAsync(request.results!);
        return Ok();
    }
    
    [HttpDelete]
    public async Task<ActionResult> ClearDownloadFilesAsync()
    {
        await cacheService.ClearVersionFilesToDownloadAsync();
        return Ok();
    }
    
    [HttpDelete]
    public async Task<ActionResult> ClearAllFilesAsync()
    {
        await cacheService.ClearVersionFilesAsync();
        return Ok();
    }
}