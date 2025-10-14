using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Database.Interfaces;

public interface IDatabaseReadService
{
    public List<ProcessRun> GetProcessRuns();
    
    Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
    
    Task<byte[]?> GetPageScreenshotAsync(int pageNumber, string fileName, string noOcrServiceName);
    
    Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request);
    
    Task<string?> GetAllPagesTextAsync(string pdfFilename, string noOcrServiceName);
    
    Task<string?> GetNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request);
    
    Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
}