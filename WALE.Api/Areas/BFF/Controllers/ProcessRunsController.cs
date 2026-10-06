using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using WALE.Api.Areas.BFF.Models;
using WALE.Api.Interfaces;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;
using WRADI.Core.AbstractionLicence.Models.ProcessRunLicenceDisplay;
using WRADI.DocumentType.AbstractionLicence.Enums;
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
    IFileService fileService,
    IMemoryCache memoryCache) : Controller
{
    [HttpGet]
    public async Task<ActionResult<IEnumerable<ProcessRun>>> GetProcessRuns()
    {
        var processRuns = await outputService.GetProcessRunsAsync();
        return Ok(processRuns.OrderByDescending(pr => pr.ProcessRunId));
    }
    
    [HttpGet]
    public ActionResult<IReadOnlyCollection<string>> GetDocumentSections()
    {
        return Ok(DocumentSectionNames.GetAll());
    }
    
    [HttpGet]
    public ActionResult<IReadOnlyCollection<string>> GetLinkReasons()
    {
        return Ok(LinkReason.GetAll());
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
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRunListAsync(
        [FromRoute] int processRunId,
        [FromQuery] ProcessRunQuery query)
    {
        var countTask = licenceListRepository.GetLicencesListSearchCountAsync(
            processRunId,
            query);

        var queryTake = query.Take;
        var querySkip = query.Skip;

        query.Skip = 0;
        query.Take = int.MaxValue;
        
        var licenceListItemsTask = licenceListRepository.GetLicencesListSearchAsync(
            processRunId,
            query);

        var issuersTask = GetDistinctListIssuers(processRunId);
        var licenceSetIdsTask = GetDistinctListLicenceSetIds(processRunId);
        var issueDatesTask = GetDistinctListDates(processRunId);
        
        var outputList = licenceListItemModelService.ConvertToOutputListDataItems(
            await licenceListItemsTask);

        var processRun = new ProcessRunResponse
        {
            TotalRecords = await countTask,
            Records = outputList.Skip(querySkip).Take(queryTake).ToList(),
            Issuers = await issuersTask,
            LicenceSetIds = await licenceSetIdsTask,
            IssueDates = await issueDatesTask,
            CumulativeFilterCounts = GetCumulativeFilterCounts(outputList, query.VerificationType)
        };
        
        var thumbnailPaths = await GetThumbnailPathsAsync(
            processRun.Records
                .Select(r => r.fileId)
                .Where(fid => fid != Guid.Empty)
                .Distinct()
                .ToList());

        foreach (var record in processRun.Records)
        {
            if (record.fileId == Guid.Empty)
            {
                continue;
            }

            var thumbnailPath = thumbnailPaths.TryGetValue(record.fileId, out var path) ? path : null;
            record.thumbnailUrl = thumbnailPath;
        }

        return Ok(processRun);
    }
    
    [HttpPost("{processRunId:int}")]
    public async Task<ActionResult> UpdateProcessRunByLicenceNumbersAsync(
        [FromRoute] int processRunId,
        [FromBody] string[] licenceNumbers)
    {
        var result = await uiProcessRunService.UpdateProcessRunByLicenceNumbersAsync(
            processRunId,
            licenceNumbers);
        
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
    
     private static CumulativeFilterCounts GetCumulativeFilterCounts(
        IReadOnlyList<OutputListDataItem> filteredData,
        string? sectionVerification)
    {
        var data = filteredData.ToList();

        return new CumulativeFilterCounts
        {
            LicenceNumbers = data.Count(x =>
                !string.IsNullOrEmpty(x.licenceNumber)),

            Purposes = data.Sum(x =>
                x.purposes?.Length ?? 0),

            Points = data.Sum(x =>
                x.points?.Length ?? 0),

            AbsLimits = data.Count(x =>
                x.limitsCount != 0),

            Aggregates = data.Count(x =>
                x.aggregatesCount > 0),

            Scans = data.Count(x =>
                x.ocr),

            IssueDates = data.Count(x =>
                !string.IsNullOrEmpty(x.issueDate)),

            Issuers = data.Count(x =>
                !string.IsNullOrEmpty(x.issuer)),

            MeansOfAbs = data.Count(x =>
                x.meansFound),

            LinkedLicences = data.Sum(x =>
                x.linkedLicences?.Length ?? 0),

            LicenceSectionVerifications = CountNonEmptyVerificationTypes(
                data,
                sectionVerification),
            
            LicenceSets = CountNonEmptyLicenceSets(
                data),
            
            Status = data.Count,
            
            Ocr =  data.Count(x =>
                 x.ocr)
        };
    }
    
    
    private static int CountNonEmptyLicenceSets(
        IEnumerable<OutputListDataItem> data)
    {
        return data.Count(item =>
            item.licenceSets != null &&
            item.licenceSets.Length > 1);
    }
    
    private static int CountNonEmptyVerificationTypes(
        IEnumerable<OutputListDataItem> data,
        string? verificationType)
    {
        var count = 0;

        foreach (var item in data)
        {
            if (item.licenceSectionVerifications == null ||
                item.licenceSectionVerifications.Length == 0)
            {
                continue;
            }

            foreach (var section in item.licenceSectionVerifications)
            {
                var sectionItems = section.LicenceSectionItems
                                   ?? [];

                if (string.IsNullOrEmpty(verificationType))
                {
                    count += sectionItems.Length;
                }
                else if (verificationType.Equals(
                             "Flagged",
                             StringComparison.OrdinalIgnoreCase))
                {
                    count += sectionItems.Count(x => x.IsFlagged);
                }
                else
                {
                    count += sectionItems.Count(x =>
                        x.VerificationTypes.Contains(
                            verificationType,
                            StringComparer.OrdinalIgnoreCase));
                }
            }
        }

        return count;
    }
    
    private async Task<Dictionary<Guid, string>> GetThumbnailPathsAsync(List<Guid> fileIds)
    {
        var templateUrl = "thumbnail_{0}.jpg";
        
        var returnDict = new Dictionary<Guid, string>();
        var fileIdChunks = fileIds.Chunk(20);

        foreach (var fileIdChunk in fileIdChunks)
        {
            var kvps = new List<(Guid, Task<string>)>();
            
            foreach (var fileId in fileIdChunk)
            {
                var lowercaseFileName = string.Format(templateUrl, fileId);
                var task = fileService.GetPresignedUrlAsync(
                    lowercaseFileName,
                    StorageFolder.Assets);
                
                kvps.Add((fileId, task));
            }

            foreach (var kvp in kvps)
            {
                var url = await kvp.Item2;
                returnDict.Add(kvp.Item1, url);
            }
        }

        return returnDict;
    }
    
    private async Task<List<Guid>> GetFileIdsAsync(int processRunId)
    {
        var fileIds = new List<Guid>();
        var loopLicences = new List<Licence>();
        
        const int licencesToTake = 10;
        var first = true;
        var loopIdx = 0;
        
        while (first || loopLicences.Count == licencesToTake)
        {
            first = false;
            var startAt = loopIdx++ * licencesToTake;
            
            loopLicences = await abstractionLicenceOutputService.GetLicencesAsync(
                processRunId,
                startAt,
                licencesToTake);
            
            var loopFileIds = loopLicences
                .Select(l => l.DmsFileId)
                .Where(fid => fid != null)
                .Select(fid => fid!.Value);
            
            fileIds.AddRange(loopFileIds);
        }
        
        var uniqueFileIds = fileIds
            .Distinct()
            .ToList();

        return uniqueFileIds;
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