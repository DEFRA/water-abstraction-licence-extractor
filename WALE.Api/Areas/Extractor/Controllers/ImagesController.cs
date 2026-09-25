using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OcrService;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class ImagesController(
    ICacheService cacheService,
    IOutputService outputService,
    IFileService fileService) : Controller
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

    [HttpPut]
    [DisableRequestSizeLimit]
    [RequestFormLimits(
        MultipartBodyLengthLimit = 1_048_576_000,
        ValueLengthLimit = 83_886_080)] // 1Gb for all files, 80Mb per file
    public async Task<ActionResult<string>> UploadAsync()
    {
        if (!Request.Form.Files.Any())
        {
            return BadRequest();
        }

        foreach (var file in Request.Form.Files)
        {
            if (!file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var lowercaseFileName = file.FileName.ToLowerInvariant();
            var fileExtension = Path.GetExtension(lowercaseFileName);

            if (!fileExtension.Equals(".jpg", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest();
            }

            string? presignedUrl;
            
            if (await fileService.ExistsAsync(lowercaseFileName, StorageFolder.Assets))
            {
                presignedUrl = await fileService.GetPresignedUrlAsync(
                    lowercaseFileName,
                    StorageFolder.Assets);
                
                return Ok(presignedUrl);
            }

            using MemoryStream stream = new();
            await file.CopyToAsync(stream);
            
            await fileService.UploadFileAsStreamAsync(
                lowercaseFileName,
                stream,
                "image/jpeg",
                StorageFolder.Assets);
            
            presignedUrl = await fileService.GetPresignedUrlAsync(
                lowercaseFileName,
                StorageFolder.Assets);

            return Ok(presignedUrl);
        }

        return Ok();
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
        if (!Request.Form.Files.Any())
        {
            return BadRequest();
        }
        
        var file = Request.Form.Files[0];

        if (!file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest();
        }
         
        using MemoryStream stream = new();
        await file.CopyToAsync(stream);
        var data = stream.ToArray();
        
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
        if (!Request.Form.Files.Any())
        {
            return BadRequest();
        }
        
        var file = Request.Form.Files[0];

        if (!file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest();
        }
         
        using MemoryStream stream = new();
        await file.CopyToAsync(stream);
        var data = stream.ToArray();
        
        await outputService.SavePageScreenshotInternalAsync(
            pageNumber,
            noOcrServiceName!,
            fileId,
            data,
            processRunId);

        return Ok();
    }
    
    [HttpPost]
    public async Task<ActionResult> SavePageScreenshotThumbnailAsync(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] string? noOcrServiceName,
        [FromQuery] int processRunId)
    {
        if (!Request.Form.Files.Any())
        {
            return BadRequest();
        }
        
        var file = Request.Form.Files[0];

        if (!file.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase)
            && !file.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest();
        }
         
        using MemoryStream stream = new();
        await file.CopyToAsync(stream);
        var data = stream.ToArray();
        
        await outputService.SavePageScreenshotThumbnailAsync(
            pageNumber,
            noOcrServiceName!,
            fileId,
            data,
            processRunId);

        return Ok();
    }
}