using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Areas.BFF.Models;
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
        var paginationData = await GetProcessRunRawDataList(processRunId, query);
        
        var getTotalsTask = abstractionLicenceOutputService.GetTotalLicenceCountAsync(processRunId, query);

        var processRun = new ProcessRunResponse
        {
            TotalRecords = await getTotalsTask,
            Records = paginationData.ToList(),
            Issuers = await GetIssuers(processRunId),
            LicenceSetIds = await GetLicenceSetIds(processRunId),
            IssueDates =  await GetIssueDates(processRunId),
        };
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
           Records = outputList.ToList(),
           Issuers = await GetDistinctListIssuers(processRunId),
           LicenceSetIds = await GetDistinctListLicenceSetIds(processRunId),
           IssueDates =  await GetDistinctListDates(processRunId),
       };
       
       return Ok(processRun);
    }

    private async Task<IReadOnlyList<OutputListDataItem>> GetProcessRunRawDataList(int processRunId, ProcessRunQuery query)
    {
        var completeNumber = 1;
        var fileNumber = 1;
        
        var verificationsBySectionTask =
            abstractionLicenceOutputService.GetVerificationLookupsBySectionNameAsync(processRunId);
        var fileIdTask = abstractionLicenceOutputService.GetLicenceFileIdsAsync(processRunId);
        
        var licences = await abstractionLicenceOutputService.GetLicencesSearchAsync(processRunId, query);
        var licenceSets =
            await abstractionLicenceOutputService.GetLicenceSetsAsync(processRunId, licences); 
        
        var verificationsBySection = await verificationsBySectionTask;
        var fileIdToLicenceNumberMapping = await fileIdTask;
        
        var paginationOutputLines = licences
            .Where(licence => licence.Status == ScrapeStatus.Ok)
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

        return paginationListData;
    }

    [HttpPost("{processRunId:int}")]
    public async Task<ActionResult> UpdateProcessRunByLicenceNumbersAsync(
        [FromRoute] int processRunId,
        [FromBody] string[] licenceNumbers)
    {
        var query = new ProcessRunQuery
        {
            Skip = 0,
            Take = int.MaxValue,
            LicenceNumbers = licenceNumbers
        };

        var processRunRawDataList = await GetProcessRunRawDataList(processRunId, query);

        await UpdateLicenceListRepo(processRunRawDataList);

        return Ok($"Updated Process Run: {processRunId} for {processRunRawDataList.Count} licences");
    }

    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult> UpdateLicenceListProcessRunAsync(
        [FromRoute] int processRunId)
    {
        var query = new ProcessRunQuery
        {
            Skip = 0,
            Take = int.MaxValue
        };

        var processRunRawDataList = await GetProcessRunRawDataList(processRunId, query);
        
        await UpdateLicenceListRepo(processRunRawDataList);

        return Ok($"Completed Process Run: {processRunId} for {processRunRawDataList.Count} licences");
    }

    [HttpGet]
    public async Task<ActionResult<int>> GetTotalLicenceCountAsync([FromQuery] int processRunId)
    {
        var total = await abstractionLicenceOutputService.GetTotalLicenceCountAsync(
            processRunId,
            new ProcessRunQuery());

        return Ok(total);
    }
    
    private async Task UpdateLicenceListRepo(IReadOnlyList<OutputListDataItem> processRunRawDataList)
    {
        var dbItems = licenceListItemModelService
            .ConvertToUpsertLicenceListItems(processRunRawDataList)
            .ToList();

        foreach (var batch in dbItems.Chunk(50))
        {
            await licenceListRepository.UpsertLicenceListItemManyAsync(
                batch,
                CancellationToken.None);
        }
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