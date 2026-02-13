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
        [FromQuery] string filename,
        [FromQuery] string noOcrServiceName)
    {
        var pageImages = await cacheService.GetImagesAsync(
            new OcrServiceImageDataCacheRequest
            {
                Filepath = filename,
                NoOcrServiceName = noOcrServiceName
            });
        
        return Ok(pageImages);
    }

    [HttpGet]
    public async Task<IActionResult> GetImageTextAsync(
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber,
        [FromQuery] string filepath,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetOcrImageTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                Filepath = filepath,
                OcrServiceName = ocrServiceName,
                ProcessRunId = processRunId
            }); 

        return Ok(imageText);
    }
    
    [HttpGet]
    public async Task<IActionResult> GetScreenshotTextAsync(
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber,
        [FromQuery] string filepath,
        [FromQuery] string ocrServiceName,
        [FromQuery] int processRunId)
    {
        var imageText = await cacheService.GetOcrScreenshotTextAsync(
            new OcrServiceImageTextCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                Filepath = filepath,
                OcrServiceName = ocrServiceName,
                ProcessRunId = processRunId
            }); 

        return Ok(imageText);
    }
    
    [HttpGet]
    public async Task<ActionResult> GetImageAsync(
        [FromQuery] string filename,
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
                Filepath = filename,
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
    public async Task<ActionResult> GetPageScreenshotAsync(
        [FromQuery] string fileName,
        [FromQuery] string serviceName,
        [FromQuery] int pageNumber)
    {
        var data = await outputService.GetPageScreenshotDataAsync(
            pageNumber,
            serviceName,
            fileName);

        return Ok(data);
    }
}