using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.PdfPig;

namespace WALE.ProcessFile.Services.Cache;

public class ApiCacheService(HttpClient httpClient) : ICacheService
{
    public bool UsesDatabase { get; set; } = true;
    public string? CacheFolder { get; set; }
    public string? Host { get; set; }
    public int Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    
    public Task SetupAsync()
    {
        throw new NotImplementedException();
    }

    public Task ClearCacheAsync(string pdfFilename)
    {
        throw new NotImplementedException();
    }

    public Task ClearCacheAsync()
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> DeflateImageAsync(string pdfFilePath, int imageNumber, int pageNumber, int processRunId, string extension,
        string serviceName)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, string pdfFilePath, string extension, string serviceName,
        int? width = null, int? height = null)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<List<(int pageNumber, int imageNumber, string extension, int width, int height)>> GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task SaveImageOnPageAsync(byte[] bytes, int width, int height, string pdfFilePath, string noOcrServiceName,
        int imageNumber, int pageNumber, string extension, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, List<Dictionary<string, object>> pagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(NoOcrServicePageCacheRequest request, List<MinimalTextBlock> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public async Task<MetadataCollection?> GetMetadataAsync(string pdfFilePath, string noOcrServiceName, int processRunId)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var path = $"/Extractor/Metadata/Get?filename={filepath}&noOcrServiceName={noOcrServiceName}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return !string.IsNullOrEmpty(content)
            ? JsonSerializer.Deserialize<MetadataCollection?>(content, JsonHelper.GetSerializerOptions())
            : null;
    }
}