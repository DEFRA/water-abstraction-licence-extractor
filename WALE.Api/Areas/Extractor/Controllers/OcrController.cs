using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class OcrController(
    ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetImageTextAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetOcrImageTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                FileId = fileId,
                OcrServiceName = ocrServiceName,
                ProcessRunId = processRunId
            }); 

        return Ok(imageText);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetScreenshotTextAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetOcrScreenshotTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = pageNumber,
                FileId = fileId,
                OcrServiceName = ocrServiceName,
                ProcessRunId = processRunId
            }); 

        return Ok(imageText);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAndSaveTemporaryImageTextAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetAndSaveTemporaryOcrImageTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                FileId = fileId,
                OcrServiceName = ocrServiceName,
                ProcessRunId = processRunId
            }); 

        var content = JsonSerializer.Serialize(imageText, JsonHelper.GetSerializerOptions());
        return Ok(content);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetAndSaveTemporaryScreenshotTextAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetAndSaveTemporaryOcrScreenshotTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = pageNumber,
                FileId = fileId,
                OcrServiceName = ocrServiceName,
                ProcessRunId = processRunId
            }); 

        return Ok(imageText);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveTemporaryOcrImageTextAsync(
        [FromBody] SaveTemporaryOcrImageTextRequest request)
    {
        var linesAndWords = JsonSerializer.Deserialize<List<LineAndWords>>(
            request.text!,
            JsonHelper.GetSerializerOptions())!;
        
        await cacheService.SaveTemporaryOcrImageTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = request.pageNumber,
                ImageNumber = request.imageNumber,
                FileId = request.fileId,
                OcrServiceName = request.ocrServiceName,
                ProcessRunId = request.processRunId
            },
            linesAndWords);

        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveTemporaryOcrScreenshotTextAsync(
        [FromBody] SaveTemporaryOcrImageTextRequest request)
    {
        var linesAndWords = JsonSerializer.Deserialize<List<LineAndWords>>(
            request.text!,
            JsonHelper.GetSerializerOptions())!;
        
        await cacheService.SaveTemporaryOcrScreenshotTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = request.pageNumber,
                FileId = request.fileId,
                OcrServiceName = request.ocrServiceName,
                ProcessRunId = request.processRunId
            },
            linesAndWords);

        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveOcrImageTextAsync(
        [FromBody] SaveOcrImageTextRequest request)
    {
        await cacheService.SaveOcrImageTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                FileId = request.fileId,
                OcrServiceName = request.ocrServiceName,
                ProcessRunId = request.processRunId,
                PageNumber = request.pageNumber,
                ImageNumber = request.imageNumber
            },
            request.pageLines!);

        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveOcrScreenshotTextAsync(
        [FromBody] SaveOcrImageTextRequest request)
    {
        await cacheService.SaveOcrScreenshotTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = request.pageNumber,
                FileId = request.fileId,
                OcrServiceName = request.ocrServiceName,
                ProcessRunId = request.processRunId
            },
            request.pageLines!);

        return Ok();
    }
}