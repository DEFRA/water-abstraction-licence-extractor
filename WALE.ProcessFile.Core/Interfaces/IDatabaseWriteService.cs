using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IDatabaseWriteService
{
    public Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun);

    public Task<int> SaveLicenceSetAsync(string licenceSetId, string shortLicenceSetId, int processRunId);

    public Task UpdateLicenceAsync(int licenceId, string licenceData, Guid fileId, int processRunId);

    public Task<int> SaveLicenceAsync(
        string? licenceNumber,
        string licenceData,
        Guid? fileId,
        string? permitNumber,
        int processRunId);

    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, string data);
    
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
    
    Task UpdateLicenceSetLicenceAsync(LicenceSetLicence licenceSetLicence);
    
    Task InsertLicenceSetLicenceAsync(int licenceSetId, int? licenceId, string? licenceNumber, string licenceVersionId, int processRunId);

    Task SaveLicenceSetTypeAsync(int licenceSetId, int licenceSetType, int processRunId);
    
    Task SaveAggregateSetAsync(int licenceSetId, string? aggregateSetAggregateSetId, string serialize, int processRunId);

    Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation);

    Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification);

    Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult);
    
    Task SaveImportRunDateAsync(string dataSource);

    Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results);
}