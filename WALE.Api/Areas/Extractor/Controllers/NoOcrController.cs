using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

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
                FileId = request.fileId,
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
                FileId = request.fileId,
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
            request.fileId,
            request.noOcrServiceName!,
            request.processRunId);

        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveNoOcrImagesMetadataAsync(
        [FromBody] SaveNoOcrImagesMetadataRequest request)
    {
        var imagesMetadata = JsonSerializer.Deserialize<ImageMetadata>(
            request.imagesMetadata!,
            JsonHelper.GetSerializerOptions())!;
        
        await cacheService.SaveNoOcrImagesMetadataAsync(
            new NoOcrServiceMetadataCacheRequest
            {
                FileId = request.fileId,
                NoOcrServiceName = request.noOcrServiceName,
                ProcessRunId = request.processRunId
            },
            imagesMetadata);

        return Ok();
    }
}

public class SaveNoOcrImagesMetadataRequest
{
    public string? imagesMetadata { get; set; }
    public Guid fileId { get; set; }
    public int processRunId { get; set; }
    public string? noOcrServiceName  { get; set; }
}

public class SaveAllPagesTextRequest
{
    public string? documentLines{ get; set; }
    public Guid fileId { get; set; }
    public string? noOcrServiceName{ get; set; }
    public int processRunId{ get; set; }
}

public class SaveNoOcrPageTextLinesRequest
{
    public Guid fileId { get; set; }
    public int pageNumber { get; set; }
    public string? noOcrServiceName  { get; set; }
    public int processRunId { get; set; }
    public string? pageLines  { get; set; }
}