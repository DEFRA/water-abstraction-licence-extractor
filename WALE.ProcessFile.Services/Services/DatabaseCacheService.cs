using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Database.Interfaces;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseCacheService(IDatabaseAddService databaseAddService) : ICacheService
{
    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }
    
    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }
    
    public Task<string?> GetNoOcrPageAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }
    
    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, string pdfFilePath, string extension)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task SaveImageAsync(byte[] bytes, string pdfFilePath, int imageNumber, int pageNumber, string extension)
    {
        throw new NotImplementedException();
    }
    
    public Task<byte[]> SaveDeflatedImageAsync(string pdfFilePath, int imageNumber, int pageNumber)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPage(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }
}