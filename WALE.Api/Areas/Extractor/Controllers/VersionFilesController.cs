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
    public async Task<ActionResult> GetToDownloadAsync([FromQuery] string licenceType)
    {
        var date =
            await abstractionLicenceCacheService.GetVersionFilesToDownloadAsync(licenceType);
        
        return Ok(date);
    }
    
    [HttpGet]
    public async Task<ActionResult> GetAllAsync([FromQuery] string licenceType)
    {
        var date = await abstractionLicenceCacheService.GetVersionFilesAsync(licenceType);
        return Ok(date);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveToDownloadAsync(
        [FromBody] VersionFilesToDownloadCreateRequest request,
        [FromQuery] string licenceType)
    {
        await abstractionLicenceCacheService.SaveVersionFilesToDownloadAsync(request.results!, licenceType);
        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveAllAsync(
        [FromBody] VersionFilesCreateRequest request,
        [FromQuery] string licenceType)
    {
        await abstractionLicenceCacheService.SaveVersionFilesAsync(request.results!, licenceType);
        return Ok();
    }
    
    [HttpDelete]
    public async Task<ActionResult> ClearDownloadFilesAsync([FromQuery] string licenceType)
    {
        await abstractionLicenceCacheService.ClearVersionFilesToDownloadAsync(licenceType);
        return Ok();
    }
    
    [HttpDelete]
    public async Task<ActionResult> ClearAllFilesAsync([FromQuery] string licenceType)
    {
        await abstractionLicenceCacheService.ClearVersionFilesAsync(licenceType);
        return Ok();
    }
}