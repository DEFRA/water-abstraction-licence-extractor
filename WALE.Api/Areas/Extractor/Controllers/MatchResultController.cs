using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class MatchResultController(IOutputService outputService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> SaveAsync([FromBody] SaveRequest request)
    {
        var matchResultId = await outputService.SaveMatchResultAsync(
            request.matches!,
            request.pdfFilename!,
            request.processRunId);
        
        return Ok(matchResultId);
    }

    public class SaveRequest
    {
        public MatchesResult? matches { get; set; }
        
        public string? pdfFilename { get; set; }
        
        public int processRunId { get; set; }
    }
}