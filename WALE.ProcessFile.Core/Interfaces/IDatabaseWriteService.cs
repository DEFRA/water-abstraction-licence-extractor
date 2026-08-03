using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay;
using WALE.ProcessFile.Core.Models.ProcessRunLicenceDisplay.DTOs;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IDatabaseWriteService
{
    public Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun);
    
    public Task<ProcessRun> MarkProcessRunCompleteIfCompleteAsync(ProcessRun processRun);
    
    public Task<ProcessRunFile> AddProcessRunFileAsync(ProcessRunFile processRunFile);

    public Task<ProcessRunFile> CompleteProcessRunFileAsync(ProcessRunFile processRunFile);
    
    public Task<ProcessRunFile> ReportErrorProcessRunFileAsync(ProcessRunFile processRunFile);
    
    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, string data);

    public Task<int> SaveStubMatchesResultAsync(string filename, Guid fileId, int processRunId);
    
    Task<int> SaveErrorMatchesResultAsync(string filename, Guid fileId, int processRunId, string? error);
    
    public Task<int> SaveMatchesResultAsync(string matchesResult, Guid fileId, int processRunId);

    public Task SavePageScreenshotAsync(
        int pageNumber,
        string noOcrServiceName,
        Guid fileId, 
        byte[] data,
        int processRunId);

    Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(NoOcrServicePageCacheRequest request, string data, int processRunId);
    
    Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr, int processRunId);
    
    Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request, string dataStr, int processRunId);
   
    Task SaveAllPagesTextAsync(string documentLinesStr, Guid fileId, string noOcrServiceName, int processRunId);

    Task SaveImageOnPageAsync(
        byte[] bytes,
        int width,
        int height,
        Guid fileId,
        string noOcrServiceName,
        int imageNumber,
        int pageNumber,
        string extension,
        int processRunId);

    Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId);
    
    Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId);
    
    Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId);
    
    Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string data, int processRunId);
    
    Task ClearCacheAsync();
    
    Task ClearCacheAsync(Guid fileId);
    
    Task UpdateProcessRunAsync(ProcessRun processRun);
    
    Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation);

    Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult);
    
    Task SaveImportRunDateAsync(string dataSource);

    Task DeleteTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request);

    Task DeleteTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);

    Task SavePageScreenshotThumbnailAsync(int pageNumber, string serviceName, Guid fileId, byte[] thumbnail,
        int processRunId);

    Task<long> UpsertLicenceListItemAsync(UpsertLicenceListItem item, CancellationToken cancellationToken = default);

    Task<IReadOnlyCollection<long>> UpsertLicenceListItemManyAsync(IReadOnlyCollection<UpsertLicenceListItem> items,
        CancellationToken cancellationToken = default);
}