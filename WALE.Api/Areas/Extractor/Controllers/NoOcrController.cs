using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class NoOcrController(
    ICacheService cacheService,
    IOutputService outputService) : Controller
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
    
    [HttpPost]
    public async Task<ActionResult> SaveNoOcrPagesMetadataAsync(
        [FromBody] SaveNoOcrPageTextLinesRequest request)
    {
        var pagesMetadata = JsonSerializer.Deserialize<List<Dictionary<string, object>>>(
            request.pageLines!,
            JsonHelper.GetSerializerOptions())!;
        
        await cacheService.SaveNoOcrPagesMetadataAsync(
            new NoOcrServiceMetadataCacheRequest
            {
                Filepath = request.filepath,
                NoOcrServiceName = request.noOcrServiceName,
                ProcessRunId = request.processRunId
            },
            pagesMetadata);

        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveAllPagesTextAsync(
        [FromBody] SaveAllPagesTextRequest request)
    {
        var documentLines = JsonSerializer.Deserialize<List<DocumentLine>>(
            request.documentLines!,
            JsonHelper.GetSerializerOptions())!;
        
        await outputService.SaveAllPagesTextAsync(
            documentLines,
            request.pdfFilePath!,
            request.noOcrServiceName!,
            request.processRunId);

        return Ok();
    }
}

public class SaveAllPagesTextRequest
{
    public string? documentLines{ get; set; }
    public string? pdfFilePath{ get; set; }
    public string? noOcrServiceName{ get; set; }
    public int processRunId{ get; set; }
}

public class SaveNoOcrPageTextLinesRequest
{
    public string? filepath { get; set; }
    public int pageNumber { get; set; }
    public string? noOcrServiceName  { get; set; }
    public int processRunId { get; set; }
    public string? pageLines  { get; set; }
}