using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Cache;

public class DatabaseCacheService(
    IDatabaseReadService databaseReadService,
    IDatabaseWriteService databaseWriteService) : ICacheService
{
    public bool UsesDatabase { get; set; } = true;

    public string? CacheFolderOrUrl { get; set; } = null;

    public Task SetupAsync()
    {
        // Nothing to do in this case
        return Task.CompletedTask;
    }

    public Task ClearCacheAsync()
    {
        return databaseWriteService.ClearCacheAsync();
    }
    
    public Task ClearCacheAsync(Guid fileId)
    {
        return databaseWriteService.ClearCacheAsync(fileId);
    }
    
    public async Task<byte[]> DeflateImageAsync(Guid fileId, int imageNumber, int pageNumber, int processRunId,  string extension, string serviceName)
    {
        var bytAry = await GetImageBytesAsync(
            new OcrServiceImageDataCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                FileId = fileId,
                Extension = extension,
                NoOcrServiceName = serviceName
            });

        if (bytAry == null)
        {
            throw new Exception("Image could not be found");
        }
        
        var deflatedBytes = ImageHelper.Deflate(bytAry);
        var pix = Pix.LoadFromMemory(deflatedBytes);
        
        await databaseWriteService.SaveImageOnPageAsync(
            deflatedBytes,
            pix.Width,
            pix.Height,
            fileId, 
            serviceName,
            imageNumber,
            pageNumber,
            "jpg",
            processRunId);
        
        return deflatedBytes;
    }
    
    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        Guid fileId,
        string extension,
        string serviceName,
        int? width = null,
        int? height = null)
    {
        return Task.FromResult(
            ImageReferenceHelper.GetImageReference(pageNumber, imageNumber, fileId, extension));
    }
    
    public Task<List<ImageDetails>> GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        return databaseReadService.GetImagesAsync(request);
    }

    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        return databaseReadService.GetImageBytesAsync(request);
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        return databaseReadService.GetNoOcrPagesMetadataAsync(request);
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        return databaseReadService.GetNoOcrImagesMetadata(request);
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        return Task.FromResult(
            ImageReferenceHelper.GetNoOcrPageReferenceAsync(
                request.FileId,
                request.NoOcrServiceName!,
                request.PageNumber));
    }
    
    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        return databaseReadService.GetNoOcrPageTextLinesAsync(request);
    }

    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request)
    {
        return databaseReadService.GetNoOcrAllPagesTextLinesAsync(request);
    }

    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        return databaseReadService.GetOcrScreenshotTextAsync(request);
    }
    
    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        return databaseReadService.GetOcrImageTextAsync(request);
    }
    
    public async Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var content = await databaseReadService.GetTemporaryOcrImageTextAsync(request);
        return JsonSerializer.Deserialize<List<LineAndWords>>(content!, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var content = await databaseReadService.GetTemporaryOcrScreenshotTextAsync(request);
        return JsonSerializer.Deserialize<List<LineAndWords>>(content!, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        var existing = await databaseReadService.GetNoOcrPagesMetadataAsync(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return request;
        }
        
        var data = new Dictionary<string, object>
        {
            { "pages", pagesMetadata },
            { "allTextFilename", "pages-all.txt" }
        };

        var dataStr = JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions());
        return await databaseWriteService.SaveNoOcrPagesMetadata(request, dataStr, request.ProcessRunId);
    }

    public async Task SaveNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        var existing = await databaseReadService.GetNoOcrImagesMetadata(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return;
        }
        
        var imagesMetadataStr = JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions());
        await databaseWriteService.SaveNoOcrImagesMetadata(request, imagesMetadataStr, request.ProcessRunId);
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLinesAsync(
        NoOcrServicePageCacheRequest request,
        string pageLines)
    {
        return await databaseWriteService.SaveNoOcrPageAsync(request, pageLines, request.ProcessRunId);
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        return databaseWriteService.SaveOcrImageTextAsync(
            request,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()),
            request.ProcessRunId);
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        return databaseWriteService.SaveOcrScreenshotTextAsync(request, pageLines, request.ProcessRunId);
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        return databaseWriteService.SaveOcrScreenshotTextAsync(
            request,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()),
            request.ProcessRunId);
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        return databaseWriteService.SaveOcrImageTextAsync(request, pageLines, request.ProcessRunId);
    }
    
    public async Task<int> SaveImageOnPageAsync(byte[] bytes, int width, int height, Guid fileId, string noOcrServiceName, int imageNumber, int pageNumber, string extension, int processRunId)
    {
        await databaseWriteService.SaveImageOnPageAsync(bytes, width, height, fileId, noOcrServiceName, imageNumber, pageNumber, extension, processRunId);

        return bytes.Length;
    }
    
    public Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        return databaseWriteService.SaveTemporaryOcrImageTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId); // TODO
    }
    
    public Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        return databaseWriteService.SaveTemporaryOcrScreenshotTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }

    public Task<MetadataCollection?> GetMetadataAsync(
        Guid fileId,
        string noOcrServiceName,
        int processRunId)
    {
        return BaseCacheService.GetMetadataAsync(
            this,
            fileId,
            noOcrServiceName,
            processRunId);
    }

    public Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync()
    {
        return databaseReadService.GetNaldLinkedLicenceRawDataAsync();
    }

    public async Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take)
    {
        var licencesTask = databaseReadService.GetNaldAbsLicencesAsync(regionCode, skip, take);
        var versionsTask = databaseReadService.GetNaldLicenceVersionsAsync(regionCode, allVersions, skip, take);
        var purposesTask = databaseReadService.GetNaldLicencePurposesAsync(regionCode, skip, take);
        var pointsTask = databaseReadService.GetNaldLicencePointsAsync(regionCode, skip, take);
        var quantitiesTask = databaseReadService.GetNaldLicenceQuantitiesAsync(regionCode, skip, take);
        var allLicencesTask = databaseReadService.GetNaldImpoundmentAndAbstractionLicencesAsync(skip, take);
        
        return new NaldDataCollection
        {
            AbstractionLicences = await licencesTask,
            AbstractionAndImpoundmentLicences = await allLicencesTask,
            AbstractionLicenceVersions = await versionsTask,
            AbstractionLicencePurposes = await purposesTask,
            AbstractionLicencePoints = await pointsTask,
            AbstractionLicenceQuantities = await quantitiesTask
        };
    }

    public Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode = null)
    {
        throw new NotImplementedException();
    }

    public Task<(
        HashSet<(string, int)> Live,
        HashSet<(string, int)> Lapsed,
        HashSet<(string, int)> Expired,
        HashSet<(string, int)> Revoked,
        HashSet<(string, int)> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode)
    {
        return databaseReadService.GetNaldLicenceNumbersAsync(regionCode);
    }

    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync()
    {
        return databaseReadService.GetDmsFileIdInformationAsync();
    }

    public Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation)
    {
        return databaseWriteService.AddDmsFileIdInformationAsync(newDmsFileIdInformation);
    }
    
    public Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult)
    {
        return databaseWriteService.SaveDmsFileReaderResultAsync(dmsFileReaderResult);
    }

    public Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber)
    {
        return databaseReadService.GetNaldLicenceIncrementNumberAsync(permitNumber, issueNumber);
    }

    public Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take)
    {
        return databaseReadService.GetDmsExtractAsync(skip, take);
    }

    public Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync()
    {
        return databaseReadService.GetDmsFileReaderResultsAsync();
    }

    public Task SaveImportRunDateAsync(string dataSource)
    {
        return databaseWriteService.SaveImportRunDateAsync(dataSource);
    }

    public Task<string?> GetImportRunDateAsync(string dataSource)
    {
        return databaseReadService.GetImportRunDateAsync(dataSource);
    }

    public Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync()
    {
        return databaseReadService.GetLicenceFinderResultsAsync();
    }

    public Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results)
    {
        return databaseWriteService.SaveLicenceFinderResultsAsync(results);
    }

    public Task ClearLicenceFinderResultsAsync()
    {
        return databaseWriteService.ClearLicenceFinderResultsAsync();
    }

    public Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync()
    {
        return databaseReadService.GetVersionFilesToDownloadAsync();
    }

    public Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results)
    {
        return databaseWriteService.SaveVersionFilesToDownloadAsync(results);
    }

    public Task<List<VersionFile>> GetVersionFilesAsync()
    {
        return databaseReadService.GetVersionFilesAsync();
    }

    public Task SaveVersionFilesAsync(List<VersionFile> results)
    {
        return databaseWriteService.SaveVersionFilesAsync(results);
    }
}