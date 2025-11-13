using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums.OutputSchema;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseReadService
{
    Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
    
    Task<byte[]?> GetPageScreenshotAsync(int pageNumber, string fileName, string noOcrServiceName);
    
    Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request);
    
    Task<string?> GetAllPagesTextAsync(string pdfFilename, string noOcrServiceName);
    
    Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request);
    
    Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request);

    Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request);

    Task<List<(int imageNumber, string extension)>> GetImagesAsync(OcrServiceImageDataCacheRequest request);
    
    Task<List<ProcessRun>> GetProcessRunsAsync();
    
    Task<ProcessRun?> GetMostRecentProcessRunAsync(string filename);
    
    Task<List<Licence>> GetLicencesAsync(int processRunId);
    
    Task<List<int>> GetLicenceSetIdsAsync(int processRunId);
    
    Task<List<LicenceSetLicence>> GetLicenceSetLicencesAsync(int licenceSetId, int processRunId);
    
    Task<LicenceSetType[]> GetLicenceSetTypes(int licenceSetId);
    
    Task<AggregateSet[]?> GetAggregateSets(int licenceSetId);
    
    Task<Licence?> GetLicenceAsync(string filename);
    
    Task<MatchesResult?> GetMatchesResult(string filename);
}