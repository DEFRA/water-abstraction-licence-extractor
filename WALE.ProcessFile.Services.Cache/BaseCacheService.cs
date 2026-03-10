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
        string pdfFilePath,
        string noOcrServiceName,
        int processRunId)
    {
        var request = new NoOcrServiceMetadataCacheRequest
        {
            Filename = pdfFilePath,
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

        return new MetadataCollection
        {
            PagesMetadata = pagesTextMetadata,
            AllDocumentLines = allDocumentLines,
            ImageMetadata = imagesMetaData
        };
    }
}