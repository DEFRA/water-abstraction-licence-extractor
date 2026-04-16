using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class ProcessRunController(IOutputService outputService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ProcessRunCreateRequest request)
    {
        var processRun = await outputService.StartProcessRunAsync(new ProcessRun
        {
            Description = request.description,
            StartDateTimeUtc = DateTime.UtcNow,
            NumberOfFiles = request.numberOfFiles
        });

        return Ok(processRun.ProcessRunId);
    }

    [HttpPost]
    public async Task<IActionResult> FinishAsync([FromBody] ProcessRunEndRequest request)
    {
        await outputService.FinishProcessRunAsync(new ProcessRun
        {
            ProcessRunId = request.processRunId
        }, request.regionCode);

        return Ok(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}