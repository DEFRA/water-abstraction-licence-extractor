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
    
    public Task<string?> GetNoOcrPageTextAsync(NoOcrServicePageCacheRequest request)
    {
        return databaseReadService.GetNoOcrPageTextAsync(request);
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

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        request.Filepath = request.Filepath!.Split('/').Last();
        
        var data = new Dictionary<string, object>
        {
            { "pages", pagesMetadata },
            { "allTextFilename", "pages-all.txt" }
        };

        var dataStr = JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions());
        return databaseAddService.SaveNoOcrPagesMetadata(request, dataStr);
    }

    public Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        request.Filepath = request.Filepath!.Split('/').Last();
        var imagesMetadataStr = JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions());
        
        return databaseAddService.SaveNoOcrImagesMetadata(request, imagesMetadataStr);
    }

    public Task SaveImageAsync(byte[] bytes, string pdfFilePath, int imageNumber, int pageNumber, string extension)
    {
        throw new NotImplementedException();
    }
    
    public Task<byte[]> SaveDeflatedImageAsync(string pdfFilePath, int imageNumber, int pageNumber)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPage(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines)
    {
        var pageLinesStr = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions());
        request.Filepath = request.Filepath!.Split('/').Last();
        
        return databaseAddService.SaveNoOcrPageAsync(request, pageLinesStr);
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }
}