using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class ProcessRunsController(IOutputService outputService, IMemoryCache memoryCache) : Controller
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessRun>>> GetProcessRuns()
    {
        var processRuns = await outputService.GetProcessRunsAsync();
        return Ok(processRuns.OrderByDescending(pr => pr.ProcessRunId));
    }
    
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessRun>>> GetAllProcessRuns()
    {
        var processRuns = await outputService.GetAllProcessRunsAsync();
        return Ok(processRuns.OrderByDescending(pr => pr.ProcessRunId));
    }
    
    [HttpGet]
    public async Task<ActionResult<Dictionary<string, LicenceSet>>> GetProcessRunLicenceSetsAsync(
        [FromQuery] int processRunId,
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue )
    {
        var licences = await outputService.GetLicencesAsync(processRunId, skip, take);
        var licenceSets = await outputService.GetLicenceSetsAsync(processRunId, licences);

        return Ok(licenceSets);
    }

    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRun(
        [FromRoute] int processRunId,
        [FromQuery] ProcessRunQuery query)
    {
        var completeNumber = 1;
        var fileNumber = 1;
        
        var allLatestLicenceSectionVerificationsTask =
            outputService.GetLatestLicenceSectionVerificationsAsync();

        var getTotalsTask = outputService.GetTotalLicenceCountAsync(processRunId, query);
        var licences = await outputService.GetLicencesSearchAsync(processRunId, query);
        var licenceSets = await outputService.GetLicenceSetsAsync(processRunId, licences); 
        
        var allLatestLicenceSectionVerifications =
            (await allLatestLicenceSectionVerificationsTask).ToList();
        
        var paginationOutputLines = licences
            .Where(licence => licence.Status == LicenceStatus.Ok)
            .Select(licence => JsOutputHelper.ToOutputLine(
                licence,
                DateTime.Now,
                completeNumber++,
                fileNumber++,
                licenceSets))
            .ToList();
        
        var paginationListData = JsOutputHelper.ToListData(
            paginationOutputLines,
            processRunId,
            allLatestLicenceSectionVerifications);

        // TODO we've really got to do this in SQL as it will be broken now
        if (!string.IsNullOrEmpty(query.ShortLicenceSetId))
        {
            paginationListData = paginationListData
                .Where(x => x.licenceSets?.Any(item => item?.ShortLicenceSetId?.Equals(query.ShortLicenceSetId) == true) == true)
                .ToList();
        }
        
        // TODO we've really got to do this in SQL as it will be broken now
        if (!string.IsNullOrEmpty(query.VerificationType))
        {
            paginationListData = paginationListData
                .Where(x => x.latestLicenceSectionVerifications?
                    .Any(item => item.VerificationType?.Equals(query.VerificationType) == true) == true)
                .ToList();
        }

        var processRun = new ProcessRunResponse
        {
            TotalRecords = await getTotalsTask,
            Records = paginationListData,
            Issuers = await GetIssuers(processRunId),
            LicenceSetIds = await GetLicenceSetIds(processRunId),
            IssueDates =  await GetIssueDates(processRunId),
        };
        
        return Ok(processRun);
    }

    [HttpGet]
    public async Task<ActionResult<int>> GetTotalLicenceCountAsync([FromQuery] int processRunId)
    {
        var total = await outputService.GetTotalLicenceCountAsync(processRunId, null);
        return Ok(total);
    }
    
    private async Task<string[]> GetLicenceSetIds(int processRunId)
    {
        var cacheKey = $"licence-set-ids:{processRunId}";

        return await memoryCache.GetOrCreateAsync(
                   cacheKey,
                   async cacheEntry =>
                   {
                       cacheEntry.AbsoluteExpirationRelativeToNow =
                           TimeSpan.FromMinutes(10);

                       var processRunQuery = new ProcessRunQuery
                       {
                           Skip = 0,
                           Take = int.MaxValue
                       };

                       var completeNumber = 1;
                       var fileNumber = 1;
                       
                       var licencesAll = await outputService.GetLicencesSearchAsync(processRunId, processRunQuery);
                       var licenceSetsAll = await outputService.GetLicenceSetsAsync(processRunId, licencesAll); 
                       
                       var paginationOutputLines = licencesAll
                           .Where(licence => licence.Status == LicenceStatus.Ok)
                           .Select(licence => JsOutputHelper.ToOutputLine(
                               licence,
                               DateTime.Now,
                               completeNumber++,
                               fileNumber++,
                               licenceSetsAll))
                           .ToList();
                       var setIds = new List<string>();

                       foreach (var set in from item in paginationOutputLines from set in item.LicenceSets?.Skip(1) where !setIds.Contains(set.ShortLicenceSetId) select set)
                       {
                           setIds.Add(set.ShortLicenceSetId);
                       }
                       
                       return setIds.ToArray();
                   })
               ?? [];
    }
    
    private async Task<string[]> GetIssuers(int processRunId)
    {
        var cacheKey = $"licence-issuers:{processRunId}";

        return await memoryCache.GetOrCreateAsync(
                   cacheKey,
                   async cacheEntry =>
                   {
                       cacheEntry.AbsoluteExpirationRelativeToNow =
                           TimeSpan.FromMinutes(10);
                       
                       var issuers =  await outputService.GetDistinctIssuersAsync(processRunId);
                       
                       return issuers.ToArray();
                   })
               ?? [];
    }
    
    private async Task<string[]> GetIssueDates(int processRunId)
    {
        var cacheKey = $"licence-issuer-dates:{processRunId}";

        return await memoryCache.GetOrCreateAsync(
                   cacheKey,
                   async cacheEntry =>
                   {
                       cacheEntry.AbsoluteExpirationRelativeToNow =
                           TimeSpan.FromMinutes(10);
                       
                       var years = await outputService.GetDistinctIssueDatesAsync(processRunId);
                       
                       return years.ToArray();
                   })
               ?? [];
    }
}