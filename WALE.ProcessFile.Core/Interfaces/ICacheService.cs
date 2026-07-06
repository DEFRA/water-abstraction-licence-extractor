using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Interfaces;

public interface ICacheService
{
    public bool UsesDatabase { get; set; }
    
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
    
    Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync();

    Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take);
    
    Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode = null);
    Task<(
            HashSet<(string, int)> Live,
            HashSet<(string, int)> Lapsed,
            HashSet<(string, int)> Expired,
            HashSet<(string, int)> Revoked,
            HashSet<(string, int)> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode);

    Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync();
    
    Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync(Guid fileId);
    
    Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation);
    
    Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber);
    
    Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take);

    Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult);

    Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync();

    Task SaveImportRunDateAsync(string dataSource);

    Task<string?> GetImportRunDateAsync(string dataSource);
    
    Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take);
    
    Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results);

    Task ClearLicenceFinderResultsAsync();
    
    Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync();

    Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results);
    
    Task<List<VersionFile>> GetVersionFilesAsync();
    
    Task SaveVersionFilesAsync(List<VersionFile> results);
    
    Task ClearVersionFilesAsync();

    Task ClearVersionFilesToDownloadAsync();

    Task<HashSet<string>> GetFirstNamesAsync();
    
    Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync();
    
    Task<NaldData?> GetNaldLicenceAsync(string permitNumber, int regionCode);
}