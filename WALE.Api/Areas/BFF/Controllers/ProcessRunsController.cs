using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
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

    [HttpGet("{processRunId:int}")]
    public async Task<ActionResult<ProcessRunResponse>> GetProcessRun(
        [FromRoute] int processRunId,
        [FromQuery] string searchTerm,
        [FromQuery] int skip = 0,
        [FromQuery] int take = int.MaxValue)
    {
        var completeNumber = 1;
        var fileNumber = 1;
        var totalLicenceCountTask = outputService.GetTotalLicenceCountAsync(processRunId, searchTerm.Equals("N/A") ? string.Empty : searchTerm);
        var allLatestLicenceSectionVerificationsTask =
            outputService.GetLatestLicenceSectionVerificationsAsync();
        var licences = await outputService.GetLicencesSearchAsync(processRunId,  searchTerm.Equals("N/A") ? string.Empty : searchTerm, skip, take);
        var licenceSets = await outputService.GetLicenceSetsAsync(processRunId, licences);
        var allLatestLicenceSectionVerifications =
            (await allLatestLicenceSectionVerificationsTask).ToList();
        
        var outputLines = licences
            .Where(licence => licence.Status == LicenceStatus.Ok)
            .Select(licence => JsOutputHelper.ToOutputLine(
                licence,
                DateTime.Now,
                completeNumber++,
                fileNumber++,
                licenceSets))
            .ToList();
        
        var listData = await JsOutputHelper.ToListDataAsync(
            outputLines,
            outputService,
            new ProcessRun
            {
                ProcessRunId = processRunId
            },
            false,
            allLatestLicenceSectionVerifications);

        var processRun = new ProcessRunResponse
        {
            TotalRecords = await totalLicenceCountTask,
            Records = listData
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