using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Cache;

public class BaseCacheService
{
    public static async Task<MetadataCollection?> GetMetadataAsync(
        ICacheService cacheService,
        Guid fileId,
        string noOcrServiceName,
        int processRunId)
    {
        var request = new NoOcrServiceMetadataCacheRequest
        {
            FileId = fileId,
            NoOcrServiceName = noOcrServiceName,
            ProcessRunId = processRunId
        };
        
        var metadataFileTextTask = cacheService.GetNoOcrPagesMetadataAsync(request);
        var metadataImagesTextTask = cacheService.GetNoOcrImagesMetadataAsync(request);
        var allDocumentLinesTask = cacheService.GetNoOcrAllPagesTextLinesAsync(request);
        
        var metadataFileText = await metadataFileTextTask;
        var metadataImagesText = await metadataImagesTextTask;
        var allDocumentLines = await allDocumentLinesTask;

        if (string.IsNullOrEmpty(metadataFileText)
            || string.IsNullOrEmpty(metadataImagesText))
        {
            return null;
        }
        
        var pagesTextMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
            metadataFileText,
            JsonHelper.GetSerializerOptions())!;
            
        var imagesMetaData = JsonSerializer.Deserialize<ImageMetadata>(
            metadataImagesText,
            JsonHelper.GetSerializerOptions())!;
        
        imagesMetaData.Pages = imagesMetaData.Pages
            .OrderBy(p => p.Number)
            .ToList();

        foreach (var page in imagesMetaData.Pages)
        {
            page.Images = page.Images
                .OrderBy(im => im
                    .Replace("-jpg-", string.Empty, StringComparison.OrdinalIgnoreCase)
                    .Replace("-png-", string.Empty, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return new MetadataCollection
        {
            PagesMetadata = pagesTextMetadata,
            AllDocumentLines = allDocumentLines,
            ImageMetadata = imagesMetaData
        };
    }
}