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
public class ProcessRunsController(
    IOutputService outputService,
    IMemoryCache memoryCache, 
    ILicenceListItemModelService licenceListItemModelService,
    ILicenceListRepository licenceListRepository) : Controller
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
        var processRun = await GetProcessRunResponse(processRunId, query);

        return Ok(processRun);
    }
    
    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRunList(
        [FromRoute] int processRunId,
        [FromQuery] ProcessRunQuery query)
    {
        
        var countTask = licenceListRepository.GetLicencesListSearchCountAsync(processRunId,  query);
        var licenceListItems = await licenceListRepository.GetLicencesListSearchAsync(processRunId, query);

       var outputList = licenceListItemModelService.ConvertToOutputListDataItems(licenceListItems);
       
       var processRun = new ProcessRunResponse
       {
           TotalRecords = await countTask,
           Records = outputList.OrderBy(x => x.licenceNumber).ToList(),
           Issuers = await GetDistinctListIssuers(processRunId),
           LicenceSetIds = await GetDistinctListLicenceSetIds(processRunId),
           IssueDates =  await GetDistinctListDates(processRunId),
       };
       return Ok(processRun);
 
    }

    private async Task<ProcessRunResponse> GetProcessRunResponse(int processRunId, ProcessRunQuery query)
    {
        var completeNumber = 1;
        var fileNumber = 1;
        
        var verificationsBySectionTask = outputService.GetVerificationLookupsBySectionNameAsync(processRunId);
        var fileIdTask = outputService.GetLicenceFileIdsAsync(processRunId);

        var getTotalsTask = outputService.GetTotalLicenceCountAsync(processRunId, query);
        var licences = await outputService.GetLicencesSearchAsync(processRunId, query);
        var licenceSets = await outputService.GetLicenceSetsAsync(processRunId, licences); 
        
        var verificationsBySection = await verificationsBySectionTask;
        var fileIdToLicenceNumberMapping = await fileIdTask;
        
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
            verificationsBySection,
            fileIdToLicenceNumberMapping);
        

        var processRun = new ProcessRunResponse
        {
            TotalRecords = await getTotalsTask,
            Records = paginationListData,
            Issuers = await GetIssuers(processRunId),
            LicenceSetIds = await GetLicenceSetIds(processRunId),
            IssueDates =  await GetIssueDates(processRunId),
        };
        return processRun;
    }

    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult<int>> UpdateLicenceListProcessRun(
        [FromRoute] int processRunId)
    {
        var query = new ProcessRunQuery
        {
            Skip = 0,
            Take = int.MaxValue
        };

        var processRun = await GetProcessRunResponse(processRunId, query);
        var dbItems = licenceListItemModelService
            .ConvertToUpsertLicenceListItems(processRun.Records)
            .ToList();
        
        foreach (var batch in dbItems.Chunk(50))
        {
            await licenceListRepository.UpsertLicenceListItemManyAsync(
                batch,
                 CancellationToken.None);
        }

        return Ok();
    }

    [HttpGet]
    public async Task<ActionResult<int>> GetTotalLicenceCountAsync([FromQuery] int processRunId)
    {
        var total = await outputService.GetTotalLicenceCountAsync(processRunId, new ProcessRunQuery());
        return Ok(total);
    }

    private async Task<string[]> GetDistinctListLicenceSetIds(int processRunId)
    {
        var cacheKey = $"licence-list-set-ids:{processRunId}";

        return await memoryCache.GetOrCreateAsync(
            cacheKey,
            async cacheEntry =>
            {
                cacheEntry.AbsoluteExpirationRelativeToNow =
                    TimeSpan.FromMinutes(10);



                var setIds = await licenceListRepository.GetLicenceListLicenceSetIdsAsync(processRunId);

                return setIds.Where(x => x.Contains('-')).OrderDescending().ToArray();
            }) ?? [];
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
                       
                       return setIds.OrderDescending().ToArray();
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
    
    private async Task<string[]> GetDistinctListIssuers(int processRunId)
    {
        var cacheKey = $"licence-list-issuers:{processRunId}";

        return await memoryCache.GetOrCreateAsync(
                   cacheKey,
                   async cacheEntry =>
                   {
                       cacheEntry.AbsoluteExpirationRelativeToNow =
                           TimeSpan.FromMinutes(10);
                       
                       var issuers =  await licenceListRepository.GetLicenceListIssuersAsync(processRunId);
                       
                       return issuers.ToArray();
                   })
               ?? [];
    }
    
    private async Task<string[]> GetDistinctListDates(int processRunId)
    {
        var cacheKey = $"licence-list-dates:{processRunId}";

        return await memoryCache.GetOrCreateAsync(
                   cacheKey,
                   async cacheEntry =>
                   {
                       cacheEntry.AbsoluteExpirationRelativeToNow =
                           TimeSpan.FromMinutes(10);
                       
                       var issuers =  await licenceListRepository.GetLicenceListIssueYearsAsync(processRunId);
                       
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