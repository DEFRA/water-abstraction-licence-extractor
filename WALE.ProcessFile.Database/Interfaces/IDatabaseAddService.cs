using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseAddService
{
    public Task<ProcessRun> AddProcessRunAsync(ProcessRun processRun);
    
    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath);
    
    public Task SaveLicenceAsync(Licence licence, string pdfFilePath);
    
    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath);
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData);

    public Task SavePageScreenshotIfDoesntExistAsync(int pageNumber, string noOcrServiceName, string pdfFilename,
        byte[] data);

    Task<NoOcrServicePageCacheRequest> SaveNoOcrPageAsync(NoOcrServicePageCacheRequest request, string pageLines);
    
    Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, string imagesMetadataStr);
    
    Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(NoOcrServiceMetadataCacheRequest request, string dataStr);
   
    Task SaveAllPagesTextIfDoesntExistAsync(string documentLinesStr, string pdfFilename, string noOcrServiceName);

    Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber,
        int pageNumber, string extension);

    Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string serialize);
}