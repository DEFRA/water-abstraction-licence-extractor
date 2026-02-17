using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.PdfPig;

namespace WALE.ProcessFile.Services.Cache;

public class MixedModeCacheService(
    ApiCacheService apiCacheService,
    DatabaseCacheService databaseCacheService)
        : ICacheService
{
    public bool UsesDatabase { get; set; } = databaseCacheService.UsesDatabase;
    public string? CacheFolderOrUrl { get; set; } = apiCacheService.CacheFolderOrUrl;

    public Task SetupAsync()
    {
        return databaseCacheService.SetupAsync();
    }

    public Task ClearCacheAsync(string pdfFilename)
    {
        return databaseCacheService.ClearCacheAsync(pdfFilename);
    }

    public Task ClearCacheAsync()
    {
        return databaseCacheService.ClearCacheAsync();
    }

    public Task<byte[]> DeflateImageAsync(
        string pdfFilePath,
        int imageNumber,
        int pageNumber,
        int processRunId,
        string extension,
        string serviceName)
    {
        return databaseCacheService.DeflateImageAsync(
            pdfFilePath,
            imageNumber,
            pageNumber,
            processRunId,
            extension,
            serviceName);
    }

    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        string pdfFilePath,
        string extension,
        string serviceName,
        int? width = null,
        int? height = null)
    {
        return databaseCacheService.GetImageReferenceAsync(
            pageNumber,
            imageNumber,
            pdfFilePath,
            extension,
            serviceName,
            width,
            height);
    }

    public Task<byte[]?> GetImageBytesAsync(
        OcrServiceImageDataCacheRequest request)
    {
        return databaseCacheService.GetImageBytesAsync(request);
    }

    public Task<List<ImageDetails>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        return apiCacheService.GetImagesAsync(request);
    }

    public Task<string> GetNoOcrPageReferenceAsync(
        NoOcrServicePageCacheRequest request)
    {
        return databaseCacheService.GetNoOcrPageReferenceAsync(request);
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request)
    {
        return databaseCacheService.GetNoOcrPagesMetadataAsync(request);
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request)
    {
        return databaseCacheService.GetNoOcrImagesMetadataAsync(request);
    }

    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(
        NoOcrServiceMetadataCacheRequest request)
    {
        return databaseCacheService.GetNoOcrAllPagesTextLinesAsync(request);
    }

    public Task<string?> GetNoOcrPageTextLinesAsync(
        NoOcrServicePageCacheRequest request)
    {
        return databaseCacheService.GetNoOcrPageTextLinesAsync(request);
    }

    public Task<string?> GetOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return apiCacheService.GetOcrImageTextAsync(request);
    }

    public Task<string?> GetOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return apiCacheService.GetOcrScreenshotTextAsync(request);
    }

    public Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return apiCacheService.GetTemporaryOcrImageTextAsync(request);
    }

    public Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request)
    {
        return apiCacheService.GetTemporaryOcrScreenshotTextAsync(request);
    }

    public Task SaveImageOnPageAsync(
        byte[] bytes,
        int width,
        int height,
        string pdfFilePath,
        string noOcrServiceName,
        int imageNumber,
        int pageNumber,
        string extension,
        int processRunId)
    {
        return databaseCacheService.SaveImageOnPageAsync(
            bytes,
            width,
            height,
            pdfFilePath,
            noOcrServiceName,
            imageNumber,
            pageNumber,
            extension,
            processRunId);
    }

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        return databaseCacheService.SaveNoOcrPagesMetadataAsync(request, pagesMetadata);
    }

    public Task SaveNoOcrImagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        ImageMetadata imagesMetadata)
    {
        return databaseCacheService.SaveNoOcrImagesMetadata(request, imagesMetadata);
    }

    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        List<MinimalTextBlock> pageLines)
    {
        return databaseCacheService.SaveNoOcrPageTextLines(request, pageLines);
    }

    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines)
    {
        return databaseCacheService.SaveOcrImageTextAsync(request, pageLines);
    }

    public Task SaveOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return databaseCacheService.SaveOcrImageTextAsync(request, pageLines);
    }

    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        string pageLines)
    {
        return databaseCacheService.SaveOcrScreenshotTextAsync(request, pageLines);
    }

    public Task SaveOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return databaseCacheService.SaveOcrScreenshotTextAsync(request, pageLines);
    }

    public Task SaveTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return apiCacheService.SaveTemporaryOcrImageTextAsync(request, pageLines);
    }

    public Task SaveTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        return apiCacheService.SaveTemporaryOcrScreenshotTextAsync(request, pageLines);
    }

    public Task<MetadataCollection?> GetMetadataAsync(string pdfFilePath, string noOcrServiceName, int processRunId)
    {
       return apiCacheService.GetMetadataAsync(pdfFilePath, noOcrServiceName, processRunId);
    }

    public Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync(int regionCode)
    {
        return apiCacheService.GetNaldLinkedLicenceRawDataAsync(regionCode);
    }

    public Task<NaldDataCollection> GetNaldDataAsync(short regionCode)
    {
        return apiCacheService.GetNaldDataAsync(regionCode);
    }

    public Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short regionCode)
    {
        return apiCacheService.GetNaldLicenceStatusDataAsync(regionCode);
    }

    public Task<(HashSet<string> Live, HashSet<string> Dead, HashSet<string> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode)
    {
        return databaseCacheService.GetNaldLicenceNumbersAsync(regionCode);
    }

    public Task<List<NaldAbstractionLicenceCsvLine>> GetNaldAbsLicencesAsync(short regionCode)
    {
        return databaseCacheService.GetNaldAbsLicencesAsync(regionCode);
    }

    public Task<List<NaldLicenceVersionCsvLine>> GetNaldLicenceVersionsAsync(short regionCode)
    {
        return databaseCacheService.GetNaldLicenceVersionsAsync(regionCode);
    }

    public Task<List<NaldLicencePurposeCsvLine>> GetNaldLicencePurposesAsync(short regionCode)
    {
        return databaseCacheService.GetNaldLicencePurposesAsync(regionCode);
    }

    public Task<List<NaldLicencePointCsvLine>> GetNaldLicencePointsAsync(short regionCode)
    {
        return databaseCacheService.GetNaldLicencePointsAsync(regionCode);
    }

    public Task<List<NaldLicenceQuantitiesCsvLine>> GetNaldLicenceQuantitiesAsync(short regionCode)
    {
        return databaseCacheService.GetNaldLicenceQuantitiesAsync(regionCode);
    }
}