using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface ICacheService
{
    public Task SetupAsync();

    public Task ClearCacheAsync(string pdfFilename);
    
    public Task ClearCacheAsync();
    
    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);
    
    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request);

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request);
    
    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request);

    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        string pdfFilePath,
        string extension);

    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request);
    
    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request);

    public Task<List<(int imageNumber, string extension)>> GetImagesAsync(OcrServiceImageDataCacheRequest request);
    
    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request);
    
    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata);
    
    public Task SaveNoOcrImagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        ImageMetadata imagesMetadata);

    public Task SaveImageOnPageAsync(byte[] bytes, string pdfFilePath, string noOcrServiceName, int imageNumber,
        int pageNumber, string extension, int processRunId);

    public Task<byte[]> SaveDeflatedImageAsync(string pdfFilePath, int imageNumber, int pageNumber, int processRunId, string extension);
    
    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines);

    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines);
    
    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines);
    
    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines);
    
    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines);
}