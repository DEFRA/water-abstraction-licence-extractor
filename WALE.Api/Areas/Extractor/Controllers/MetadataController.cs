using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Api.Areas.Extractor.Controllers;

[ApiController]
[Area("Extractor")]
[Route("/[area]/[controller]/[action]")]
public class MetadataController(ICacheService cacheService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> GetAsync(
        [FromQuery] Guid fileId,
        [FromQuery] string noOcrServiceName)
    {
        var request = new NoOcrServiceMetadataCacheRequest
        {
            FileId = fileId,
            NoOcrServiceName = noOcrServiceName
        };
        
        var metadataFileTextTask = cacheService.GetNoOcrPagesMetadataAsync(request);
        var metadataImagesTextTask = cacheService.GetNoOcrImagesMetadataAsync(request);
        var allDocumentLinesTask = cacheService.GetNoOcrAllPagesTextLinesAsync(request);
        
        var metadataFileText = await metadataFileTextTask;
        var metadataImagesText = await metadataImagesTextTask;

        if (string.IsNullOrEmpty(metadataFileText)
            || string.IsNullOrEmpty(metadataImagesText))
        {
            return Ok("null");
        }
        
        var pagesTextMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
            metadataFileText,
            JsonHelper.GetSerializerOptions())!;
            
        var imagesMetaData = JsonSerializer.Deserialize<ImageMetadata>(
            metadataImagesText,
            JsonHelper.GetSerializerOptions())!;
        
        return Ok(new MetadataCollection
        {
            PagesMetadata = pagesTextMetadata,
            AllDocumentLines = await allDocumentLinesTask,
            ImageMetadata = imagesMetaData
        });
    }
}