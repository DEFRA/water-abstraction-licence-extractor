using Microsoft.AspNetCore.Mvc;
using WALE.Api.Areas.BFF.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Services;

namespace WALE.Api.Areas.BFF.Controllers;

[ApiController]
[Area("BFF")]
[Route("/[area]/[controller]/[action]")]
public class ImagesController(IOutputService outputService, ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<ActionResult> Thumbnail([FromQuery] string filename)
    {
        var parts = filename.Split('/');
        var fileName1 = parts[0];
        var serviceName = parts[1];

        var pageNumberStr = parts.Last()
            .Replace("page-", string.Empty)
            .Replace(".jpg", string.Empty);

        var pageNumber = int.Parse(pageNumberStr);
        var data = await outputService.GetPageScreenshotDataAsync(
            pageNumber,
            serviceName,
            fileName1);

        if (data == null)
        {
            throw new Exception($"Cannot find screenshot for {fileName1} - {serviceName} - {pageNumber}");
        }

        return File(data, "image/jpeg");
    }

    [HttpGet]
    public async Task<ActionResult> Image([FromQuery] string filename)
    {
        var parts = filename.Split('/');
        var fileName1 = parts[0];
        var serviceName = parts[1];

        var pageNumberStr = parts.Last()
            .Replace("page-", string.Empty)
            .Replace(".jpg", string.Empty);

        var pageNumber = int.Parse(pageNumberStr);
        var data = await outputService.GetPageScreenshotDataAsync(
            pageNumber,
            serviceName,
            fileName1);

        return File(data!, "image/jpeg");
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<PageImage>>> PageImages([FromQuery] string filename,
        [FromQuery] int? pageNumber)
    {
        var pageImages = await cacheService.GetImagesAsync(new OcrServiceImageDataCacheRequest
        {
            PageNumber = pageNumber,
            Filepath = filename,
            NoOcrServiceName = PdfDataExtractorService.Name
        });

        var pageImagesUnique = pageImages
            .GroupBy(pi => pi.imageNumber)
            .Select(pi => pi.Last())
            .OrderBy(pi => pi.imageNumber)
            .Select(pi => new PageImage
            {
                PageNumber = pi.pageNumber,
                ImageNumber = pi.imageNumber,
                Extension = pi.extension,
                FileName = filename,
                Width = pi.width,
                Height = pi.height
            });
        
        return Ok(pageImagesUnique);
    }
    
    [HttpGet]
    public async Task<ActionResult> PartialPageImage([FromQuery] string filename, [FromQuery] string extension,
        [FromQuery] int pageNumber, [FromQuery] int imageNumber)
    {
        var bytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = filename,
            NoOcrServiceName = PdfDataExtractorService.Name,
            Extension = extension
        });

        if (bytes == null)
        {
            return NotFound();
        }
    
        return File(bytes, "image/jpeg");
    }
}