using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Interfaces;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class MatchResultController(IOutputService outputService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> SaveAsync([FromBody] SaveMatchResultRequest request)
    {
        var matchResultId = await outputService.SaveMatchResultAsync(
            request.matches!,
            request.fileId,
            request.processRunId);
        
        return Ok(matchResultId);
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveStubAsync([FromBody] SaveStubMatchResultRequest request)
    {
        var matchResultId = await outputService.SaveStubMatchesResultAsync(
            request.filename!,
            request.fileId,
            request.processRunId);
        
        return Ok(matchResultId);
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveStubFinishAsync([FromBody] SaveStubMatchResultRequest request)
    {
        var matchResultId = await outputService.SaveStubFinishMatchesResultAsync(
            request.filename!,
            request.fileId,
            request.processRunId);
        
        return Ok(matchResultId);
    }
    
    [HttpPost]
    public async Task<IActionResult> SaveErrorAsync([FromBody] SaveErrorMatchResultRequest request)
    {
        var matchResultId = await outputService.SaveErrorMatchesResultAsync(
            request.filename!,
            request.fileId,
            request.processRunId,
            request.error);
        
        return Ok(matchResultId);
    }
}