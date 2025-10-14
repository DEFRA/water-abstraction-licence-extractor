using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseAddService
{
    public Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun);

    public Task SaveLicenceSetAsync(string licenceSet, string licenceSetId, string shortLicenceSetId);
    
    public Task SaveLicenceAsync(string licence, string pdfFilePath);
    
    public Task SaveMatchResultAsync(string matchesResult, string pdfFilePath);

    public Task SavePageScreenshotIfDoesntExistAsync(int pageNumber, string noOcrServiceName, string pdfFilename,
        byte[] data);

    Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(NoOcrServicePageCacheRequest request, string pageLines);
    
    Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr);
    
    Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request, string dataStr);
   
    Task SaveAllPagesTextIfDoesntExistAsync(string documentLinesStr, string pdfFilename, string noOcrServiceName);

    Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber,
        int pageNumber, string extension);

    Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string serialize);
    
    Task ClearCacheAsync();
    
    Task ClearCacheAsync(string pdfFilename);
    
    Task UpdateProcessRunAsync(ProcessRun processRun);
}