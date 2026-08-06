using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Areas.BFF.Models;
using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.DocumentType.AbstractionLicence.Helpers;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class ProcessRunsController(
    IOutputService outputService,
    IAbstractionLicenceOutputService abstractionLicenceOutputService,
    ILicenceListItemModelService licenceListItemModelService,
    ILicenceListRepository licenceListRepository,
    IUiProcessRunService uiProcessRunService,
    IMemoryCache memoryCache) : Controller
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
        var licences = await abstractionLicenceOutputService.GetLicencesAsync(
            processRunId,
            skip,
            take);
        
        var licenceSets = await abstractionLicenceOutputService.GetLicenceSetsAsync(
            processRunId,
            licences);

        return Ok(licenceSets);
    }
    
    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRun(
        [FromRoute] int processRunId,
        [FromQuery] ProcessRunQuery query)
    { 
        var getTotalsTask =
            abstractionLicenceOutputService.GetTotalLicenceCountAsync(
                processRunId,
                query);

        var paginationDataTask =
           uiProcessRunService.GetProcessRunRawDataList(
                processRunId,
                query);

        var issuersTask =
            GetIssuers(processRunId);

        var licenceSetIdsTask =
            GetLicenceSetIds(processRunId);

        var issueDatesTask =
            GetIssueDates(processRunId);

        await Task.WhenAll(
            getTotalsTask,
            paginationDataTask,
            issuersTask,
            licenceSetIdsTask,
            issueDatesTask);

        var processRun = new ProcessRunResponse
        {
            TotalRecords = await getTotalsTask,
            Records = (await paginationDataTask).ToList(),
            Issuers = await issuersTask,
            LicenceSetIds = await licenceSetIdsTask,
            IssueDates = await issueDatesTask
        };
        
        return Ok(processRun);
    }

    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRunList(
        [FromRoute] int processRunId,
        [FromQuery] ProcessRunQuery query)
    {
        var countTask =
            licenceListRepository.GetLicencesListSearchCountAsync(
                processRunId,
                query);

        var licenceListItemsTask =
            licenceListRepository.GetLicencesListSearchAsync(
                processRunId,
                query);

        var issuersTask =
            GetDistinctListIssuers(processRunId);

        var licenceSetIdsTask =
            GetDistinctListLicenceSetIds(processRunId);

        var issueDatesTask =
            GetDistinctListDates(processRunId);

        await Task.WhenAll(
            countTask,
            licenceListItemsTask,
            issuersTask,
            licenceSetIdsTask,
            issueDatesTask);

        var licenceListItems = await licenceListItemsTask;

        var outputList =
            licenceListItemModelService
                .ConvertToOutputListDataItems(licenceListItems);

        var processRun = new ProcessRunResponse
        {
            TotalRecords = await countTask,
            Records = outputList.ToList(),
            Issuers = await issuersTask,
            LicenceSetIds = await licenceSetIdsTask,
            IssueDates = await issueDatesTask
        };

        return Ok(processRun);
    }
    
    [HttpPost("{processRunId:int}")]
    public async Task<ActionResult> UpdateProcessRunByLicenceNumbersAsync(
        [FromRoute] int processRunId,
        [FromBody] string[] licenceNumbers)
    {
        var result = await uiProcessRunService.UpdateProcessRunByLicenceNumbersAsync(processRunId, licenceNumbers);  
        return Ok(result);
    }

    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult> UpdateLicenceListProcessRunAsync(
        [FromRoute] int processRunId)
    {
        var result = await uiProcessRunService.UpdateLicenceListProcessRunAsync(processRunId);  
        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<int>> GetTotalLicenceCountAsync([FromQuery] int processRunId)
    {
        var total = await abstractionLicenceOutputService.GetTotalLicenceCountAsync(
            processRunId,
            new ProcessRunQuery());

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
                       
                       var licencesAll = await abstractionLicenceOutputService.GetLicencesSearchAsync(processRunId, processRunQuery);
                       var licenceSetsAll = await abstractionLicenceOutputService.GetLicenceSetsAsync(processRunId, licencesAll); 
                       
                       var paginationOutputLines = licencesAll
                           .Where(licence => licence.Status == ScrapeStatus.Ok)
                           .Select(licence => JsOutputHelper.ToOutputLine(
                               licence,
                               DateTime.Now,
                               completeNumber++,
                               fileNumber++,
                               licenceSetsAll))
                           .ToList();
                       var setIds = new List<string>();

                       foreach (var set 
                            in from item 
                            in paginationOutputLines from set
                            in item.LicenceSets!.Skip(1)
                            where !setIds.Contains(set.ShortLicenceSetId)
                            select set)
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

                var issuers =  await abstractionLicenceOutputService.GetDistinctIssuersAsync(processRunId);

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
           
           var years = await abstractionLicenceOutputService.GetDistinctIssueDatesAsync(processRunId);
           
           return years.ToArray();
        })
        ?? [];
    }
}