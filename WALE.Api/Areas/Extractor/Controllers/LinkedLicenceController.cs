using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LinkedLicenceController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetMapAsync()
    {
        var naldLinkedLicenceRawData =
            await cacheService.GetNaldLinkedLicenceRawDataAsync();
        
        return Ok(naldLinkedLicenceRawData);
    }
}