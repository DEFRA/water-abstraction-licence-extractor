using Microsoft.AspNetCore.Mvc;
using SkiaSharp;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class ImagesController(
    IOutputService outputService,
    ICacheService cacheService) : Controller
{
    [HttpGet]
    [ResponseCache(VaryByHeader = "User-Agent", Duration = int.MaxValue)]
    public async Task<ActionResult> Thumbnail(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] string serviceName)
    {
        var thumbnail = await outputService.GetPageScreenshotThumbnailAsync(
            pageNumber,
            serviceName,
            fileId);

        if (thumbnail != null)
        {
            return File(thumbnail, "image/jpeg");
        }
        
        var data = await outputService.GetPageScreenshotDataAsync(
            pageNumber,
            serviceName,
            fileId);

        var originalResImage = SKImage.FromEncodedData(data[0]);
        var originalRegBitmap = SKBitmap.FromImage(originalResImage);
        var resizedBitmap = originalRegBitmap.Resize(
            new SKSizeI(120, 160),
            SKSamplingOptions.Default);

        var resizedImage = SKImage.FromBitmap(resizedBitmap);
        var resizedJpg = resizedImage.Encode(SKEncodedImageFormat.Jpeg, 60);

        thumbnail = resizedJpg.AsSpan().ToArray();
        await outputService.SavePageScreenshotThumbnailAsync(
            pageNumber,
            serviceName,
            fileId,
            thumbnail,
            -1);

        return File(thumbnail, "image/jpeg");
    }

    [HttpGet]
    public async Task<ActionResult> Image(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] string serviceName)
    {
        var data = await outputService.GetPageScreenshotDataAsync(
            pageNumber,
            serviceName,
            fileId);

        return File(data[0], "image/jpeg");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PageImage>>> PageImages(
        [FromQuery] Guid fileId,
        [FromQuery] int? pageNumber)
    {
        var pageImages = await cacheService.GetImagesAsync(
            new OcrServiceImageDataCacheRequest
            {
                PageNumber = pageNumber,
                FileId = fileId,
                NoOcrServiceName = GeneralConstants.PdfPigDataExtractorServiceName
            });

        var pageImagesUnique = pageImages
            .GroupBy(pi => new { pi.pageNumber, pi.imageNumber })
            .Select(pi => pi.Last())
            .OrderBy(pi => pi.imageNumber)
            .Select(pi => new PageImage
            {
                PageNumber = pi.pageNumber,
                ImageNumber = pi.imageNumber,
                Extension = pi.extension!,
                FileId = fileId,
                Width = pi.width,
                Height = pi.height
            });
        
        return Ok(pageImagesUnique);
    }
    
    [HttpGet]
    public async Task<ActionResult> PartialPageImage(
        [FromQuery] Guid fileId,
        [FromQuery] string extension,
        [FromQuery] int pageNumber,
        [FromQuery] int imageNumber)
    {
        var bytes = await cacheService.GetImageBytesAsync(
            new OcrServiceImageDataCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                FileId = fileId,
                NoOcrServiceName = GeneralConstants.PdfPigDataExtractorServiceName,
                Extension = extension
            });

        if (bytes == null)
        {
            return NotFound();
        }
    
        return File(bytes, "image/jpeg");
    }
}