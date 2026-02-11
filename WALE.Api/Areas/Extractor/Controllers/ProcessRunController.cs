using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class ProcessRunController(IOutputService outputService) : Controller
{
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] CreateRequest request)
    {
        var processRun = await outputService.SaveProcessRunAsync(new ProcessRun
        {
            Description = request.description,
            StartDateTimeUtc = DateTime.UtcNow,
            NumberOfFiles = request.numberOfFiles
        });

        return Ok(processRun.ProcessRunId);
    }
    
    public class CreateRequest
    {
        public string? description { get; set; }
        public int numberOfFiles { get; set; }
    }
}