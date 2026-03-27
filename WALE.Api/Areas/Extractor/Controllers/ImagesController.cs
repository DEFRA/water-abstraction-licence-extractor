using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.Extractor.Controllers.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class ImagesController(
    ICacheService cacheService,
    IOutputService outputService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(
        [FromQuery] Guid fileId,
        [FromQuery] string noOcrServiceName)
    {
        var pageImages = await cacheService.GetImagesAsync(
            new OcrServiceImageDataCacheRequest
            {
                FileId = fileId,
                NoOcrServiceName = noOcrServiceName
            });
        
        return Ok(pageImages);
    }
    
    [HttpGet]
    public async Task<ActionResult> GetImageAsync(
        [FromQuery] Guid fileId,
        [FromQuery] string extension,
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber,
        [FromQuery] string? noOcrServiceName)
    {
        var bytes = await cacheService.GetImageBytesAsync(
            new OcrServiceImageDataCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                FileId = fileId,
                NoOcrServiceName = noOcrServiceName,
                Extension = extension
            });

        if (bytes == null)
        {
            return NotFound();
        }
    
        return File(bytes, "image/jpeg");
    }

    [HttpGet]
    public async Task<ActionResult> DeflateImageAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int imageNumber,
        [FromQuery] int pageNumber,
        [FromQuery] int processRunId,
        [FromQuery] string extension,
        [FromQuery] string serviceName)
    {
        var bytes = await cacheService.DeflateImageAsync(
            fileId,
            imageNumber,
            pageNumber,
            processRunId,
            extension,
            serviceName);

        return File(bytes, "image/jpeg");
    }
    
    [HttpGet]
    public async Task<ActionResult> GetPageScreenshotAsync(
        [FromQuery] Guid fileId,
        [FromQuery] string serviceName,
        [FromQuery] int pageNumber)
    {
        var data = await outputService.GetPageScreenshotDataAsync(
            pageNumber,
            serviceName,
            fileId);

        return Ok(data);
    }
    
    [HttpPost]
    public async Task<ActionResult> SaveImageOnPageAsync(
        [FromBody] SaveImageOnPageRequest request)
    {
        await cacheService.SaveImageOnPageAsync(
            request.bytes,
            request.width,
            request.height,
            request.fileId,
            request.noOcrServiceName!,
            request.imageNumber,
            request.pageNumber,
            request.extension!,
            request.processRunId);

        return Ok(request.bytes.Length);
    }
    
    [HttpPost]
    public async Task<ActionResult> SavePageScreenshotAsync(
        [FromBody] SavePageScreenshotRequest request)
    {
        await outputService.SavePageScreenshotInternalAsync(
            request.pageNumber,
            request.noOcrServiceName!,
            request.fileId,
            request.data,
            request.processRunId);

        return Ok();
    }
}