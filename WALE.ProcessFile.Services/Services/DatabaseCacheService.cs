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

    public Task ClearCacheAsync()
    {
        return databaseAddService.ClearCacheAsync();
    }
    
    public Task ClearCacheAsync(string pdfFilename)
    {
        return databaseAddService.ClearCacheAsync(pdfFilename);
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetNoOcrPagesMetadataAsync(request);
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetNoOcrImagesMetadata(request);
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return Task.FromResult($"NoOcrPageReference-{pdfFilename}-{request.NoOcrServiceName}-{request.PageNumber}");
    }
    
    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetNoOcrPageTextLinesAsync(request);
    }
    
    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, string pdfFilePath, string extension)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        return Task.FromResult($"ImageReference-{pdfFilename}-{extension}-{pageNumber}-{imageNumber}");
    }

    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetOcrImageTextAsync(request);
    }

    public Task<List<(int imageNumber, string extension)>> GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetImagesAsync(request);
    }

    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetImageBytesAsync(request);
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);

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
        return await databaseAddService.SaveNoOcrPagesMetadata(request, dataStr, request.ProcessRunId);
    }

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
     
        var existing = await databaseReadService.GetNoOcrImagesMetadata(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return;
        }
        
        var imagesMetadataStr = JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions());
        await databaseAddService.SaveNoOcrImagesMetadata(request, imagesMetadataStr, request.ProcessRunId);
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);

        var existing = await databaseReadService.GetNoOcrPageTextLinesAsync(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return request;
        }
        
        var pageLinesStr = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions());
        return await databaseAddService.SaveNoOcrPageAsync(request, pageLinesStr, request.ProcessRunId);
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseAddService.SaveOcrImageTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseAddService.SaveOcrImageTextAsync(request, pageLines, request.ProcessRunId);
    }
    
    public Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber, int pageNumber, string extension, int processRunId)
    {
        var filename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        return databaseAddService.SaveImageOnPageAsync(bytes, filename, noOcrServiceName, imageNumber, pageNumber, extension, processRunId);
    }
    
    public Task<byte[]> SaveDeflatedImageAsync(string pdfFilePath, int imageNumber, int pageNumber)
    {
        throw new NotImplementedException();
    }
}