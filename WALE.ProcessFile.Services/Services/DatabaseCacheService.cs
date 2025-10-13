using System.Text.Json;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseCacheService(
    IDatabaseReadService databaseReadService,
    IDatabaseAddService databaseAddService) : ICacheService
{
    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }
    
    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        return databaseReadService.GetNoOcrPagesMetadataAsync(request);
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        var pdfFilename = request.Filepath!.Split('/').Last();
        return Task.FromResult($"NoOcrPageReference-{pdfFilename}-{request.NoOcrServiceName}-{request.PageNumber}");
    }
    
    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        return databaseReadService.GetNoOcrPageTextLinesAsync(request);
    }
    
    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, string pdfFilePath, string extension)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        request.Filepath = request.Filepath!.Split('/').Last();

        var existing = await databaseReadService.GetNoOcrPagesMetadataAsync(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return request;
        }
        
        var data = new Dictionary<string, object>
        {
            { "pages", pagesMetadata },
            { "allTextFilename", "pages-all.txt" }
        };

        var dataStr = JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions());
        return await databaseAddService.SaveNoOcrPagesMetadata(request, dataStr);
    }

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        request.Filepath = request.Filepath!.Split('/').Last();
     
        var existing = await databaseReadService.GetNoOcrImagesMetadata(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return;
        }
        
        var imagesMetadataStr = JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions());
        await databaseAddService.SaveNoOcrImagesMetadata(request, imagesMetadataStr);
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines)
    {
        request.Filepath = request.Filepath!.Split('/').Last();

        var existing = await databaseReadService.GetNoOcrPageTextLinesAsync(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return request;
        }
        
        var pageLinesStr = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions());
        return await databaseAddService.SaveNoOcrPageAsync(request, pageLinesStr);
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveImageAsync(byte[] bytes, string pdfFilePath, int imageNumber, int pageNumber, string extension)
    {
        throw new NotImplementedException();
    }
    
    public Task<byte[]> SaveDeflatedImageAsync(string pdfFilePath, int imageNumber, int pageNumber)
    {
        throw new NotImplementedException();
    }
}