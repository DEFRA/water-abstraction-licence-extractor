using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ICacheService
{
    public bool UsesDatabase { get; set; }
    
    public string? CacheFolderOrUrl { get; set; }
    
    public Task SetupAsync();

    public Task ClearCacheAsync(string pdfFilename);
    
    public Task ClearCacheAsync();
    
    public Task<byte[]> DeflateImageAsync(string pdfFilePath, int imageNumber, int pageNumber, int processRunId, string extension, string serviceName);

    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        string pdfFilePath,
        string extension,
        string serviceName,
        int? width = null,
        int? height = null);
    
    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request);
    
    public Task<List<ImageDetails>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request);
    
    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request);
    
    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
    
    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);

    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request);
    
    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request);

    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
    
    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request);
    
    Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request);

    public Task SaveImageOnPageAsync(
        byte[] bytes,
        int width,
        int height,
        string pdfFilePath,
        string noOcrServiceName,
        int imageNumber,
        int pageNumber,
        string extension,
        int processRunId);
    
    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata);
    
    public Task SaveNoOcrImagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        ImageMetadata imagesMetadata);
    
    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        string pageLines);

    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines);
    
    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines);
    
    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines);
    
    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines);

    Task SaveTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines);
    
    Task SaveTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines);

    Task<MetadataCollection?> GetMetadataAsync(
        string pdfFilePath,
        string noOcrServiceName,
        int processRunId);
    
    Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync(int regionCode);

    Task<NaldDataCollection> GetNaldDataAsync(short regionCode);
    
    Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short regionCode);
    
    Task<(HashSet<string> Live, HashSet<string> Dead, HashSet<string> Impoundment)> 
        GetNaldLicenceNumbersAsync(short? regionCode);
}