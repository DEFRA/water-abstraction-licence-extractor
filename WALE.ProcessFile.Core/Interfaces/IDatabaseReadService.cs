using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.OutputSchema.Table;

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
    
    Task<ProcessRun?> GetMostRecentProcessRunAsync(Guid fileId);
    
    Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take);
    
    Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(int processRunId);
    
    Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(Guid fileId, int processRunId);
    
    Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int processRunId);
    
    Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId);
    
    Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId);
    
    Task<List<(int LicenceSetId, LicenceSetType Type)>> GetLicenceSetTypesForProcessRun(int processRunId);
    
    Task<AggregateSet[]?> GetAggregateSets(int licenceSetId);
    
    Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(int processRunId);
    
    Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId);
    
    Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId);
    
    Task<MatchesResult?> GetMatchesResult(Guid fileId);

    Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync();

    Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync(int skip, int take);

    Task<(
        HashSet<(string, int)> Live,
        HashSet<(string, int)> Lapsed,
        HashSet<(string, int)> Expired,
        HashSet<(string, int)> Revoked,
        HashSet<(string, int)> Impoundment)> GetNaldLicenceNumbersAsync(short? regionCode);

    Task<List<NaldAbstractionLicenceDataLine>> GetNaldAbsLicencesAsync(short? regionCode, int skip, int take);

    Task<List<NaldLicenceVersionDataLine>> GetNaldLicenceVersionsAsync(short? regionCode, bool allVersions, int skip, int take);

    Task<List<NaldLicencePurposeDataLine>> GetNaldLicencePurposesAsync(short? regionCode, int skip, int take);

    Task<List<NaldLicencePointDataLine>> GetNaldLicencePointsAsync(short? regionCode, int skip, int take);

    Task<List<NaldLicenceQuantitiesDataLine>> GetNaldLicenceQuantitiesAsync(short? regionCode, int skip, int take);
    
    Task<Licence?> GetNewestLicenceAsync(string permitNumber);
    
    Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber);

    Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId);

    Task<IEnumerable<LicenceSectionVerification>> GetLatestLicenceSectionVerificationsAsync();

    Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take);

    Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync();
    
    Task<string?> GetImportRunDateAsync(string dataSource);

    Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take);
    
    Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync();
    
    Task<List<VersionFile>> GetVersionFilesAsync();
    
    Task<byte[]?> GetPageScreenshotThumbnailAsync(int pageNumber, Guid fileId, string noOcrServiceName);
}