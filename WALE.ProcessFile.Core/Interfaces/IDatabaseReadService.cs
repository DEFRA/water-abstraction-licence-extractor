using WALE.ProcessFile.Core.Enums.OutputSchema;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.OutputSchema.Table;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IDatabaseReadService
{
    Task<List<SharePointFileIdInformation>> GetSharePointFileIdInformationAsync();
    
    Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
    
    Task<byte[]?> GetPageScreenshotAsync(int pageNumber, string fileName, string noOcrServiceName);
    
    Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request);
    
    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request);
    
    Task<string?> GetAllPagesTextAsync(string pdfFilename, string noOcrServiceName);
    
    Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request);
    
    Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<string?> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
    
    Task<string?> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);

    Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request);
    
    Task<List<ImageDetails>> GetImagesAsync(OcrServiceImageDataCacheRequest request);
    
    Task<List<ProcessRun>> GetProcessRunsAsync();
    
    Task<ProcessRun?> GetMostRecentProcessRunAsync(string filename);
    
    Task<List<Licence>> GetLicencesAsync(int processRunId);
    
    Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(int processRunId);
    
    Task<List<LicenceSetTable>> GetLicenceSetsSimpleAsync(string filename, int processRunId);
    
    Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int processRunId);
    
    Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId);
    
    Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId);
    
    Task<List<(int LicenceSetId, LicenceSetType Type)>> GetLicenceSetTypesForProcessRun(int processRunId);
    
    Task<AggregateSet[]?> GetAggregateSets(int licenceSetId);
    
    Task<List<(int LicenceSetId, AggregateSet AggregateSet)>> GetAggregateSetsForProcessRun(int processRunId);
    
    Task<Licence?> GetLicenceAsync(string filename);
    
    Task<Licence?> GetLicenceAsync(string licenceNumber, int processRunId);
    
    Task<MatchesResult?> GetMatchesResult(string filename);

    Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync();

    Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync();

    Task<(
        HashSet<(string, int)> Live,
        HashSet<(string, int)> Lapsed,
        HashSet<(string, int)> Expired,
        HashSet<(string, int)> Revoked,
        HashSet<(string, int)> Impoundment)> GetNaldLicenceNumbersAsync(short? regionCode);

    Task<List<NaldAbstractionLicenceDataLine>> GetNaldAbsLicencesAsync(short? regionCode);

    Task<List<NaldLicenceVersionDataLine>> GetNaldLicenceVersionsAsync(short? regionCode);

    Task<List<NaldLicencePurposeDataLine>> GetNaldLicencePurposesAsync(short? regionCode);

    Task<List<NaldLicencePointDataLine>> GetNaldLicencePointsAsync(short? regionCode);

    Task<List<NaldLicenceQuantitiesDataLine>> GetNaldLicenceQuantitiesAsync(short? regionCode);
    
    Task<Licence?> GetNewestLicenceAsync(string permitNumber);
}