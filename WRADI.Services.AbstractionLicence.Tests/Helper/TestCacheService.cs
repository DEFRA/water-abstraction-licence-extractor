using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Core.Models.NoOcrService;
using WALE.ProcessFile.Core.Models.OcrService;

namespace WRADI.Services.AbstractionLicence.Tests.Helper;

public class TestCacheService : ICacheService
{
    public string? CacheFolderOrUrl { get; set; }
    public Task SetupAsync()
    {
        throw new NotImplementedException();
    }

    public Task ClearCacheAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task ClearCacheAsync()
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> DeflateImageAsync(Guid fileId, int imageNumber, int pageNumber, int processRunId, string extension,
        string serviceName)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, Guid fileId, string extension, string serviceName,
        int? width = null, int? height = null)
    {
        throw new NotImplementedException();
    }

    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<List<ImageDetails>> GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<List<LineAndWords>> GetAndSaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<List<LineAndWords>> GetAndSaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<int> SaveImageOnPageAsync(byte[] bytes, int width, int height, Guid fileId, string noOcrServiceName, int imageNumber,
        int pageNumber, string extension, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, List<Dictionary<string, object>> pagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task SaveNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public Task<MetadataCollection?> GetMetadataAsync(Guid fileId, string noOcrServiceName, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync(Guid fileId)
    {
        return Task.FromResult(new List<DmsFileIdInformation>());
    }

    public Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation)
    {
        return Task.CompletedTask;
    }

    public Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take)
    {
        throw new NotImplementedException();
    }

    public Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult)
    {
        throw new NotImplementedException();
    }

    public Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync()
    {
        throw new NotImplementedException();
    }

    public Task SaveImportRunDateAsync(string dataSource)
    {
        throw new NotImplementedException();
    }

    public Task<string?> GetImportRunDateAsync(string dataSource)
    {
        throw new NotImplementedException();
    }

    public Task<HashSet<string>> GetFirstNamesAsync()
    {
        throw new NotImplementedException();
    }

    public Task<DmsFileData?> GetDmsFileDataAsync(string? licenceNumber)
    {
        throw new NotImplementedException();
    }
}