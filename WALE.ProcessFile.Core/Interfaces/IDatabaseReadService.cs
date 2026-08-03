using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.OutputSchema.Table;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IDatabaseReadService
{
    Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync();
    
    Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
    
    Task<byte[]?> GetPageScreenshotAsync(int pageNumber, Guid fileId, string noOcrServiceName);
    
    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request);
    
    Task<string?> GetAllPagesTextAsync(Guid fileId, string noOcrServiceName);
    
    Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request);
    
    Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<string?> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<string?> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);

    Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request);
    
    Task<List<ImageDetails>> GetImagesAsync(OcrServiceImageDataCacheRequest request);
    
    Task<List<ProcessRun>> GetProcessRunsAsync();
    
    Task<List<ProcessRun>> GetAllProcessRunsAsync();
    
    Task<ProcessRun?> GetMostRecentProcessRunAsync(Guid fileId);
    
    Task<MatchesResult?> GetMatchesResult(Guid fileId);
    
    Task<MatchesResult?> GetMatchesResult(Guid fileId, int processRunId);
    
    Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take);

    Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync();
    
    Task<string?> GetImportRunDateAsync(string dataSource);
    
    Task<byte[]?> GetPageScreenshotThumbnailAsync(int pageNumber, Guid fileId, string noOcrServiceName);
    
    Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync(Guid fileId);
    
    Task<DmsFileData?> GetDmsFileDataAsync(string? licenceNumber);

    Task<List<string>> GetDistinctIssuersAsync(int processRunId);
    
    Task<List<string>> GetDistinctIssueDatesAsync(int processRunId);

    Task<List<LicenceListItemAggregate>> GetLicencesListSearchAsync(
        int processRunId,
        ProcessRunQuery query,
        CancellationToken cancellationToken = default);
    
    Task<List<string>> GetLicenceListDistinctIssuersAsync(int processRunId);
    
    Task<List<string>> GetLicenceListLicenceSetIdsAsync(int processRunId);
    
    Task<List<string>> GetLicenceListIssueYearsAsync(int processRunId);

    Task<Dictionary<Guid, string>> GetLicenceFileIdsAsync(int processRunId);

    Task<int> GetLicencesListSearchCountAsync(
        int processRunId,
        ProcessRunQuery query);
}