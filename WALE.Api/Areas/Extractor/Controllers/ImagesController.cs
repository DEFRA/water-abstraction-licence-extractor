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
    
    [HttpPost]
    public async Task<ActionResult> SaveImageOnPageAsync(
        [FromBody] SaveImageOnPageRequest request)
    {
        await cacheService.SaveImageOnPageAsync(
            request.bytes,
            request.width,
            request.height,
            request.pdfFilePath!,
            request.noOcrServiceName!,
            request.imageNumber,
            request.pageNumber,
            request.extension!,
            request.processRunId);

        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SavePageScreenshotAsync(
        [FromBody] SavePageScreenshotRequest request)
    {
        await outputService.SavePageScreenshotInternalAsync(
            request.pageNumber,
            request.noOcrServiceName!,
            request.pdfFilename!,
            request.data,
            request.processRunId);

        return Ok();
    }
    
    public class SavePageScreenshotRequest
    {
        public int pageNumber { get; set; }
        public string? noOcrServiceName { get; set; }
        public string? pdfFilename { get; set; }
        public byte[] data { get; set; }
        public int processRunId { get; set; }
    }
    
    public class SaveImageOnPageRequest
    {
        public byte[] bytes { get; set; }

        public int width { get; set; }

        public int height { get; set; }

        public string? pdfFilePath { get; set; }

        public string? noOcrServiceName { get; set; }

        public int imageNumber { get; set; }

        public int pageNumber { get; set; }

        public string? extension { get; set; }

        public int processRunId { get; set; }
    }
}