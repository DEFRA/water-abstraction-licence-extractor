using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class NaldDataController(
    IAbstractionLicenceCacheService abstractionLicenceCacheService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] short? regionCode = null,
        [FromQuery] bool? allVersions = null,
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var naldData = await abstractionLicenceCacheService.GetNaldDataAsync(
            regionCode,
            allVersions ?? false,
            skip,
            take);
        
        return Ok(naldData);
    }

    [OutputCache(Duration=60)] // Doesn't change often at all
    [HttpGet]
    public async Task<IActionResult> GetImpoundmentAndAbstractionLicencesAsync()
    {
        var naldLicences =
            await abstractionLicenceCacheService.GetNaldImpoundmentAndAbstractionLicencesAsync();
        return Ok(naldLicences);
    }

    [HttpGet]
    public async Task<IActionResult> GetLicenceStatusDataAsync([FromQuery] short? regionCode = null)
    {
        var naldLicenceNumbers =
            await abstractionLicenceCacheService.GetNaldLicenceNumbersAsync(regionCode);

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
        var incrementNumber = await abstractionLicenceCacheService.GetNaldLicenceIncrementNumberAsync(
            permitNumber,
            issueNumber);

        return Ok(incrementNumber);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] string licenceNumber,
        [FromQuery] int regionCode,
        [FromQuery] bool slashesRemoved)
    {
        var naldData = await abstractionLicenceCacheService.GetNaldAbstractionLicenceAsync(
            licenceNumber,
            regionCode,
            slashesRemoved);
        
        return Ok(naldData);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetImpoundmentAsync(
        [FromQuery] string licenceNumber,
        [FromQuery] int regionCode)
    {
        var naldData = await abstractionLicenceCacheService.GetNaldImpoundmentLicenceAsync(licenceNumber, regionCode);
        return Ok(naldData);
    }

    [HttpGet]
    public async Task<IActionResult> GetNaldLicenceNumberHistoryAsync()
    {
        var history =
            await abstractionLicenceCacheService.GetNaldLicenceNumberHistoryAsync();
        
        return Ok(history);
    }

    [HttpGet]
    public async Task<IActionResult> GetDocumentNaldPurposeMap()
    {
        var data = await abstractionLicenceOutputService.GetDocumentNaldPurposeMapAsync();
        return Ok(data);
    }
    
    [HttpPost]
    public async Task<IActionResult> AddDocumentNaldPurposeMap(AddDocumentNaldPurposeMapRequest request)
    {
        await abstractionLicenceOutputService.AddDocumentNaldPurposeMapAsync(
            request.documentDescription!,
            request.naldPurpose!,
            request.matchType!);
        
        return Ok();
    }
    
    [HttpPost]
    public async Task<IActionResult> AddDocumentNaldPurposeMatch(AddDocumentNaldPurposeMatchRequest request)
    {
        await abstractionLicenceOutputService.AddDocumentNaldPurposeMatchAsync(
            request.licNo!,
            request.documentDescription!,
            request.naldPurpose!,
            request.matchType!);
        
        return Ok();
    }
}