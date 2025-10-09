using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface ICacheService
{
    public Task SetupAsync();
    
    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
    
    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);

    public Task<string?> GetNoOcrPageAsync(NoOcrServicePageCacheRequest request);

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata);
    
    public Task SaveNoOcrImagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        ImageMetadata imagesMetadata);

    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        string pdfFilePath,
        string extension);
    
    public Task SaveImageAsync(byte[] bytes, string pdfFilePath, int imageNumber, int pageNumber, string extension);
    
    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPage(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines);
}