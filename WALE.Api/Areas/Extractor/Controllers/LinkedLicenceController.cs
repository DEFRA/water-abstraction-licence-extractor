using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LinkedLicenceController(ICacheService cacheService) : Controller
{
    [OutputCache(Duration=60)] // Doesn't change often at all
    [HttpGet]
    public async Task<IActionResult> GetMapAsync()
    {
        var naldLinkedLicenceRawData =
            await cacheService.GetNaldLinkedLicenceRawDataAsync();
        
        return Ok(naldLinkedLicenceRawData);
    }
}