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
public class OcrController(
    ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetImageTextAsync(
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber,
        [FromQuery] Guid fileId,
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
    public async Task<IActionResult> GetTemporaryImageTextAsync(
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber,
        [FromQuery] Guid fileId,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetTemporaryOcrImageTextAsync(
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
    public async Task<IActionResult> GetScreenshotTextAsync(
        [FromQuery] int pageNumber,
        [FromQuery] Guid fileId,
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
    public async Task<IActionResult> GetTemporaryScreenshotTextAsync(
        [FromQuery] int pageNumber,
        [FromQuery] Guid fileId,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetTemporaryOcrScreenshotTextAsync(
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
    
    public class SaveOcrImageTextRequest
    {
        public Guid fileId { get; set; }
        public int pageNumber { get; set; }
        public int imageNumber { get; set; }
        public string? ocrServiceName  { get; set; }
        public int processRunId { get; set; }
        public string? pageLines { get; set; }
    }
    
    public class SaveTemporaryOcrImageTextRequest
    {
        public Guid fileId { get; set; }
        
        public int processRunId { get; set; }
        
        public int pageNumber { get; set; }
        
        public int imageNumber { get; set; }
        
        public string? ocrServiceName { get; set; }
        
        public string? text { get; set; }
    }
}