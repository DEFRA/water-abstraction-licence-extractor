using Microsoft.AspNetCore.Mvc;
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
        [FromQuery] Guid fileId,
        [FromQuery] int width,
        [FromQuery] int height,
        [FromQuery] string? noOcrServiceName,
        [FromQuery] int imageNumber,
        [FromQuery] int pageNumber,
        [FromQuery] string? extension,
        [FromQuery] int processRunId)
    {
        var data = new byte[Request.ContentLength!.Value];
        _ = await Request.Body.ReadAsync(data);
        
        await cacheService.SaveImageOnPageAsync(
            data,
            width,
            height,
            fileId,
            noOcrServiceName!,
            imageNumber,
            pageNumber,
            extension!,
            processRunId);

        return Ok(data.Length);
    }
    
    [HttpPost]
    public async Task<ActionResult> SavePageScreenshotAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] string? noOcrServiceName,
        [FromQuery] int processRunId)
    {
        var data = new byte[Request.ContentLength!.Value];
        _ = await Request.Body.ReadAsync(data);
        
        await outputService.SavePageScreenshotInternalAsync(
            pageNumber,
            noOcrServiceName!,
            fileId,
            data,
            processRunId);

        return Ok();
    }
}