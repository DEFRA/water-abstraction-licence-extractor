using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WRADI.Core.AbstractionLicence.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class VersionFilesController(
    IAbstractionLicenceCacheService abstractionLicenceCacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> GetToDownloadAsync()
    {
        var date =
            await abstractionLicenceCacheService.GetVersionFilesToDownloadAsync();
        
        return Ok(date);
    }
    
    [HttpGet]
    public async Task<ActionResult> GetAllAsync()
    {
        var date = await abstractionLicenceCacheService.GetVersionFilesAsync();
        return Ok(date);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveToDownloadAsync(
        [FromBody] VersionFilesToDownloadCreateRequest request)
    {
        await abstractionLicenceCacheService.SaveVersionFilesToDownloadAsync(request.results!);
        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveAllAsync(
        [FromBody] VersionFilesCreateRequest request)
    {
        await abstractionLicenceCacheService.SaveVersionFilesAsync(request.results!);
        return Ok();
    }
    
    [HttpDelete]
    public async Task<ActionResult> ClearDownloadFilesAsync()
    {
        await abstractionLicenceCacheService.ClearVersionFilesToDownloadAsync();
        return Ok();
    }
    
    [HttpDelete]
    public async Task<ActionResult> ClearAllFilesAsync()
    {
        await abstractionLicenceCacheService.ClearVersionFilesAsync();
        return Ok();
    }
}