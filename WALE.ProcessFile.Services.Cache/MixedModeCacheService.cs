using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.PdfPig;

namespace WALE.ProcessFile.Services.Cache;

public class MixedModeCacheService(
    ApiCacheService apiCacheService,
    DatabaseCacheService databaseCacheService)
        : ICacheService
{
    public bool UsesDatabase { get; set; } = databaseCacheService.UsesDatabase;
    public string? CacheFolder { get; set; }
    public string? Host { get; set; } = databaseCacheService.Host;
    public int Port { get; set; } = databaseCacheService.Port;
    public string? DatabaseName { get; set; } = databaseCacheService.DatabaseName;
    public string? Username { get; set; } = databaseCacheService.Username;
    public string? Password { get; set; } = databaseCacheService.Password;

    public Task SetupAsync()
    {
        return databaseCacheService.SetupAsync();
    }

    public Task ClearCacheAsync(string pdfFilename)
    {
        return databaseCacheService.ClearCacheAsync(pdfFilename);
    }

    public Task ClearCacheAsync()
    {
        return databaseCacheService.ClearCacheAsync();
    }

    public Task<byte[]> DeflateImageAsync(
        string pdfFilePath,
        int imageNumber,
        int pageNumber,
        int processRunId,
        string extension,
        string serviceName)
    {
        return databaseCacheService.DeflateImageAsync(
            pdfFilePath,
            imageNumber,
            pageNumber,
            processRunId,
            extension,
            serviceName);
    }

    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        string pdfFilePath,
        string extension,
        string serviceName,
        int? width = null,
        int? height = null)
    {
        return databaseCacheService.GetImageReferenceAsync(
            pageNumber,
            imageNumber,
            pdfFilePath,
            extension,
            serviceName,
            width,
            height);
    }

    public Task<byte[]?> GetImageBytesAsync(
        OcrServiceImageDataCacheRequest request)
    {
        return databaseCacheService.GetImageBytesAsync(request);
    }

    public Task<List<(int pageNumber, int imageNumber, string extension, int width, int height)>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        return databaseCacheService.GetImagesAsync(request);
    }

    public Task<string> GetNoOcrPageReferenceAsync(
        NoOcrServicePageCacheRequest request)
    {
        return databaseCacheService.GetNoOcrPageReferenceAsync(request);
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request)
    {
        return databaseCacheService.GetNoOcrPagesMetadataAsync(request);
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request)
    {
        return databaseCacheService.GetNoOcrImagesMetadataAsync(request);
    }

    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(
        NoOcrServiceMetadataCacheRequest request)
    {
        return databaseCacheService.GetNoOcrAllPagesTextLinesAsync(request);
    }

    public Task<string?> GetNoOcrPageTextLinesAsync(
        NoOcrServicePageCacheRequest request)
    {
        return databaseCacheService.GetNoOcrPageTextLinesAsync(request);
    }

    public Task<string?> GetOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return databaseCacheService.GetOcrImageTextAsync(request);
    }

    public Task<string?> GetOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return databaseCacheService.GetOcrScreenshotTextAsync(request);
    }

    public Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return databaseCacheService.GetTemporaryOcrImageTextAsync(request);
    }

    public Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return databaseCacheService.GetTemporaryOcrScreenshotTextAsync(request);
    }

    public Task SaveImageOnPageAsync(
        byte[] bytes,
        int width,
        int height,
        string pdfFilePath,
        string noOcrServiceName,
        int imageNumber,
        int pageNumber,
        string extension,
        int processRunId)
    {
        return databaseCacheService.SaveImageOnPageAsync(
            bytes,
            width,
            height,
            pdfFilePath,
            noOcrServiceName,
            imageNumber,
            pageNumber,
            extension,
            processRunId);
    }

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        return databaseCacheService.SaveNoOcrPagesMetadataAsync(request, pagesMetadata);
    }

    public Task SaveNoOcrImagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        ImageMetadata imagesMetadata)
    {
        return databaseCacheService.SaveNoOcrImagesMetadata(request, imagesMetadata);
    }

    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        List<MinimalTextBlock> pageLines)
    {
        return databaseCacheService.SaveNoOcrPageTextLines(request, pageLines);
    }

    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines)
    {
        return databaseCacheService.SaveOcrImageTextAsync(request, pageLines);
    }

    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return databaseCacheService.SaveOcrImageTextAsync(request, pageLines);
    }

    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines)
    {
        return databaseCacheService.SaveOcrScreenshotTextAsync(request, pageLines);
    }

    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return databaseCacheService.SaveOcrScreenshotTextAsync(request, pageLines);
    }

    public Task SaveTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return databaseCacheService.SaveTemporaryOcrImageTextAsync(request, pageLines);
    }

    public Task SaveTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return databaseCacheService.SaveTemporaryOcrScreenshotTextAsync(request, pageLines);
    }

    public Task<MetadataCollection?> GetMetadataAsync(string pdfFilePath, string noOcrServiceName, int processRunId)
    {
       return apiCacheService.GetMetadataAsync(pdfFilePath, noOcrServiceName, processRunId);
    }
}