using Microsoft.AspNetCore.Mvc;
using SkiaSharp;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OcrService;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class ImagesController(
    IOutputService outputService,
    ICacheService cacheService,
    IImageService imageService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> Image(
        [FromQuery] Guid fileId,
        [FromQuery] int pageNumber,
        [FromQuery] string serviceName)
    {
        // Redirect to S3 directly when this row has been migrated (see WRADI-377); the browser
        // follows the redirect transparently for an <img src>, no frontend change needed. Falls
        // back to proxying Postgres bytes for rows not yet backfilled to S3.
        var s3Key = ImageReferenceHelper.GetPageScreenshotS3Key(fileId, serviceName, pageNumber);

        if (await imageService.ExistsAsync(s3Key))
        {
            return Redirect(await imageService.GetPresignedUrlAsync(s3Key));
        }

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
        // Same S3-redirect-first, Postgres-proxy-fallback shape as Image() above.
        var s3Key = ImageReferenceHelper.GetImageOnPageS3Key(
            fileId, GeneralConstants.PdfPigDataExtractorServiceName, pageNumber, imageNumber, extension);

        if (await imageService.ExistsAsync(s3Key))
        {
            return Redirect(await imageService.GetPresignedUrlAsync(s3Key));
        }

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