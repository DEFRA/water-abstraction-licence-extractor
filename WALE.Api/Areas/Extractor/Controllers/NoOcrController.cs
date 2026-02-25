using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class NoOcrController(
    ICacheService cacheService) : Controller
{
    [HttpPost]
    public async Task<ActionResult> SaveNoOcrPageTextLinesAsync(
    [FromBody] SaveNoOcrPageTextLinesRequest request)
    {
        await cacheService.SaveNoOcrPageTextLinesAsync(
            new NoOcrServicePageCacheRequest
            {
                Filepath = request.filepath,
                PageNumber = request.pageNumber,
                NoOcrServiceName = request.noOcrServiceName,
                ProcessRunId = request.processRunId
            },
            request.pageLines!);

        return Ok();
    }
}

public class SaveNoOcrPageTextLinesRequest
{
    public string? filepath { get; set; }
    public int pageNumber { get; set; }
    public string? noOcrServiceName  { get; set; }
    public int processRunId { get; set; }
    public string? pageLines  { get; set; }
}