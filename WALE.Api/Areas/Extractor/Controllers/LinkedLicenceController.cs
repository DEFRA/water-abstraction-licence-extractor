using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WRADI.Core.AbstractionLicence.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class LinkedLicenceController(
    IAbstractionLicenceCacheService abstractionLicenceCacheService) : Controller
{
    [OutputCache(Duration=60)] // Doesn't change often at all
    [HttpGet]
    public async Task<IActionResult> GetMapAsync()
    {
        var naldLinkedLicenceRawData =
            await abstractionLicenceCacheService.GetNaldLinkedLicenceRawDataAsync();
        
        return Ok(naldLinkedLicenceRawData);
    }
}