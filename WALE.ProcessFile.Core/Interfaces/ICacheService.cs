using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ICacheService
{
    public string? CacheFolderOrUrl { get; set; }
    
    public Task SetupAsync();

    public Task ClearCacheAsync(Guid fileId);
    
    public Task ClearCacheAsync();
    
    public Task<byte[]> DeflateImageAsync(Guid fileId, int imageNumber, int pageNumber, int processRunId, string extension, string serviceName);

    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        Guid fileId,
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
    
    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
    
    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<List<LineAndWords>> GetAndSaveTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request);
    
    Task<List<LineAndWords>> GetAndSaveTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request);

    public Task<int> SaveImageOnPageAsync(
        byte[] bytes,
        int width,
        int height,
        Guid fileId,
        string noOcrServiceName,
        int imageNumber,
        int pageNumber,
        string extension,
        int processRunId);
    
    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata);
    
    public Task SaveNoOcrImagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        ImageMetadata imagesMetadata);
    
    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLinesAsync(
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
        Guid fileId,
        string noOcrServiceName,
        int processRunId);
    
    Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync();
    
    Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync(Guid fileId);
    
    Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation);
    
    Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take);

    Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult);

    Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync();

    Task SaveImportRunDateAsync(string dataSource);

    Task<string?> GetImportRunDateAsync(string dataSource);
    
    Task<HashSet<string>> GetFirstNamesAsync();
    
    Task<DmsFileData?> GetDmsFileDataAsync(string? licenceNumber);
}