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
    public async Task<IActionResult> SaveAsync([FromBody] SaveMatchResultRequest matchResultRequest)
    {
        var matchResultId = await outputService.SaveMatchResultAsync(
            matchResultRequest.matches!,
            matchResultRequest.fileId!,
            matchResultRequest.processRunId);
        
        return Ok(matchResultId);
    }
}