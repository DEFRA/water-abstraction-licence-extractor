using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class NaldDataController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] short regionCode)
    {
        var naldData = await cacheService.GetNaldDataAsync(regionCode);
        return Ok(naldData);
    }

    [HttpGet]
    public async Task<IActionResult> GetLicenceStatusDataAsync([FromQuery] short regionCode)
    {
        var naldLicenceNumbers = await cacheService.GetNaldLicenceNumbersAsync(
            regionCode);

        return Ok(new NaldLicenceStatusData
        {
            LiveLicences = naldLicenceNumbers.Live
                .Select(l => FormattingHelper.StripForComparison(l, regionCode))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet(),
            DeadLicences = naldLicenceNumbers.Dead
                .Select(l => FormattingHelper.StripForComparison(l, regionCode))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet(),
            ImpoundmentLicences = naldLicenceNumbers.Impoundment
                .Select(l => FormattingHelper.StripForComparison(l, regionCode))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet()
        });
    }
}