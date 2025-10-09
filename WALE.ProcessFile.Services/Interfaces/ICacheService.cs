using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface ICacheService
{
    public Task SetupAsync();
    
    public Task<string?> GetNoOcrMetadataAsync(NoOcrServiceMetadataCacheRequest request);

    public Task<string?> GetNoOcrPageAsync(NoOcrServicePageCacheRequest request);

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata);
    
    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPage(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines);
}