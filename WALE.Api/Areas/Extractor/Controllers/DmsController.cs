using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class DmsController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetFileDataAsync(
        [FromQuery] string? licenceNumber,
        [FromQuery] int regionCode)
    {
        var dmsFileData = await cacheService.GetDmsFileDataAsync(licenceNumber, regionCode);
        return Ok(dmsFileData);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetFileIdsAsync()
    {
        var dmsFileIdInformation =
            await cacheService.GetDmsFileIdInformationAsync();
        
        return Ok(dmsFileIdInformation);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetFileIdInformationAsync([FromQuery] Guid fileId)
    {
        var dmsFileIdInformation =
            await cacheService.GetDmsFileIdInformationAsync(fileId);
        
        return Ok(dmsFileIdInformation);
    }
    
    [HttpPost]
    public async Task<ActionResult> AddFileIdInformationAsync(
        [FromBody] DmsFileIdInformation request)
    {
        await cacheService.AddDmsFileIdInformationAsync(request);
        return Ok();
    }
    
    [HttpGet]
    public async Task<IActionResult> GetExtractAsync(
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var dmsExtract = await cacheService.GetDmsExtractAsync(skip, take);
        return Ok(dmsExtract);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveDmsFileReaderResultAsync(
        [FromBody] DmsFileReaderResult request)
    {
        await cacheService.SaveDmsFileReaderResultAsync(request);
        return Ok();
    }
    
    [HttpGet]
    public async Task<ActionResult> GetDmsFileReaderResultsAsync()
    {
        var dmsFileReaderResults = await cacheService.GetDmsFileReaderResultsAsync();
        return Ok(dmsFileReaderResults);
    }
}