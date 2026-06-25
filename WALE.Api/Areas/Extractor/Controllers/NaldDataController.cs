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
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] short? regionCode = null,
        [FromQuery] bool? allVersions = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var naldData = await cacheService.GetNaldDataAsync(
            regionCode,
            allVersions ?? false,
            skip,
            take);
        
        return Ok(naldData);
    }

    [HttpGet]
    public async Task<IActionResult> GetLicenceStatusDataAsync([FromQuery] short? regionCode = null)
    {
        var naldLicenceNumbers = await cacheService.GetNaldLicenceNumbersAsync(
            regionCode);

        return Ok(new NaldLicenceStatusData
        {
            LiveLicences = naldLicenceNumbers.Live
                .Select(l => FormattingHelper.StripForComparison(l.Item1, l.Item2))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet(),
            LapsedLicences = naldLicenceNumbers.Lapsed
                .Select(l => FormattingHelper.StripForComparison(l.Item1, l.Item2))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet(),
            ExpiredLicences = naldLicenceNumbers.Expired
                .Select(l => FormattingHelper.StripForComparison(l.Item1, l.Item2))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet(),
            RevokedLicences = naldLicenceNumbers.Revoked
                .Select(l => FormattingHelper.StripForComparison(l.Item1, l.Item2))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet(),            
            ImpoundmentLicences = naldLicenceNumbers.Impoundment
                .Select(l => FormattingHelper.StripForComparison(l.Item1, l.Item2))
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => x!)
                .ToHashSet()
        });
    }
    
    [HttpGet]
    public async Task<IActionResult> GetCurrentIncrementNumberAsync(
        [FromQuery] string permitNumber,
        [FromQuery] int issueNumber)
    {
        var incrementNumber = await cacheService.GetNaldLicenceIncrementNumberAsync(permitNumber, issueNumber);
        return Ok(incrementNumber);
    }
}