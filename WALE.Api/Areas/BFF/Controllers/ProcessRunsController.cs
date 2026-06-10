using Microsoft.AspNetCore.Mvc;
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
    public async Task<ActionResult<IReadOnlyList<OutputListDataItem>>> GetProcessRun([FromRoute] int processRunId)
    {
        var completeNumber = 1;
        var fileNumber = 1;

        var licences = await outputService.GetLicencesAsync(processRunId);
        var licenceSetsTasks = outputService.GetLicenceSetsAsync(processRunId, licences);
        var allLatestLicenceSectionVerifications =
            (await outputService.GetLatestLicenceSectionVerificationsAsync()).ToList();
        var licenceSets = await licenceSetsTasks;
        
        var outputLines = licences
            .Where(licence => licence.Status == LicenceStatus.Ok)
            .Select(licence => JsOutputHelper.ToOutputLine(
                licence,
                DateTime.Now,
                completeNumber++,
                fileNumber++,
                licenceSets))
            .ToList();

        // TODO look into the speed tommorrow
        
        var listData = await JsOutputHelper.ToListDataAsync(
            outputLines,
            outputService,
            new ProcessRun
            {
                ProcessRunId = processRunId
            },
            false,
            allLatestLicenceSectionVerifications);

        return Ok(listData);
    }
}