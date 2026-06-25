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
            NumberOfFiles = request.numberOfFiles,
            Status = request.status
        });

        return Ok(processRun.ProcessRunId);
    }
    
    [HttpPost]
    public async Task<IActionResult> MarkProcessRunCompleteAsync([FromBody] ProcessRunEndRequest request)
    {
        var processRun = await outputService.MarkProcessRunCompleteAsync(new ProcessRun
        {
            ProcessRunId = request.processRunId,
        });

        return Ok(processRun);
    }
    
    [HttpPost]
    public async Task<IActionResult> AddProcessRunFileAsync([FromBody] ProcessRunFileRequest request)
    {
        var processRunFile = await outputService.AddProcessRunFileAsync(new ProcessRunFile
        {
            FileName = request.FileName,
            ProcessRunId = request.ProcessRunId
        });

        return Ok(processRunFile.ProcessRunFileId);
    }
    
    [HttpPost]
    public async Task<IActionResult> CompleteProcessRunFileAsync([FromBody] ProcessRunFileRequest request)
    {
        var processRunFile = await outputService.CompleteProcessRunFileAsync(new ProcessRunFile
        {
            ProcessRunFileId = request.ProcessRunFileId,
            FileName = request.FileName,
            ProcessRunId = request.ProcessRunId
        });

        return Ok(processRunFile.ProcessRunFileId);
    }
    
    
    [HttpPost]
    public async Task<IActionResult> ReportErrorProcessRunFileAsync([FromBody] ProcessRunFileRequest request)
    {
        var processRunFile = await outputService.ReportErrorProcessRunFileAsync(new ProcessRunFile
        {
            ProcessRunFileId = request.ProcessRunFileId,
            FileName = request.FileName,
            ProcessRunId = request.ProcessRunId,
            ErrorMessage = request.ErrorMessage
        });

        return Ok(processRunFile.ProcessRunFileId);
    }
 
    [HttpPost]
    public async Task<IActionResult> FinishAsync([FromBody] ProcessRunEndRequest request)
    {
        await outputService.FinishProcessRunAsync(new ProcessRun
        {
            ProcessRunId = request.processRunId
        });

        return Ok(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
    }
}