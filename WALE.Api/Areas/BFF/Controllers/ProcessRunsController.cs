using Microsoft.AspNetCore.Mvc;
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
public class ProcessRunsController(IOutputService outputService) : Controller
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
    [ResponseCache(VaryByHeader = "User-Agent", Duration = int.MaxValue)]
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRun(
        [FromRoute] int processRunId,
        [FromQuery] ProcessRunQuery query)
    {
        var take = query.Take;
        var skip = query.Skip;
        query.Take = int.MaxValue;
        query.Skip = 0;
        
        var completeNumber = 1;
        var fileNumber = 1;
        
        var allLatestLicenceSectionVerificationsTask =
            outputService.GetLatestLicenceSectionVerificationsAsync();
        
        var licencesAll = await outputService.GetLicencesSearchAsync(processRunId, query);
        var licenceSetsAll = await outputService.GetLicenceSetsAsync(processRunId, licencesAll);
        
        var allLatestLicenceSectionVerifications =
            (await allLatestLicenceSectionVerificationsTask).ToList();
        
        var paginationOutputLines = licencesAll
            .Where(licence => licence.Status == LicenceStatus.Ok)
            .Select(licence => JsOutputHelper.ToOutputLine(
                licence,
                DateTime.Now,
                completeNumber++,
                fileNumber++,
                licenceSetsAll))
            .ToList();
        
        var paginationListData = JsOutputHelper.ToListData(
            paginationOutputLines,
            processRunId,
            allLatestLicenceSectionVerifications);

        if (!string.IsNullOrEmpty(query.ShortLicenceSetId))
        {
            paginationListData = paginationListData
                .Where(x => x.licenceSets?.Any(item => item?.ShortLicenceSetId?.Equals(query.ShortLicenceSetId) == true) == true)
                .ToList();
        }
        
        if (!string.IsNullOrEmpty(query.VerificationType))
        {
            paginationListData = paginationListData
                .Where(x => x.latestLicenceSectionVerifications?
                    .Any(item => item.VerificationType?.Equals(query.VerificationType) == true) == true)
                .ToList();
        }

        var processRun = new ProcessRunResponse
        {
            TotalRecords = paginationListData.Count,
            Records = paginationListData
                .Skip(skip)
                .Take(take)
                .ToList(),
            NoPaginationRecords = paginationListData,
        };
        
        return Ok(processRun);
    }

    [HttpGet]
    public async Task<ActionResult<int>> GetTotalLicenceCountAsync([FromQuery] int processRunId)
    {
        var total = await outputService.GetTotalLicenceCountAsync(processRunId, null);
        return Ok(total);
    }
}