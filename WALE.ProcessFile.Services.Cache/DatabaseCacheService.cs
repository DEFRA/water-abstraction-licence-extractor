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
    
    public Task ClearCacheAsync(string pdfFilename)
    {
        return databaseWriteService.ClearCacheAsync(pdfFilename);
    }
    
    public async Task<byte[]> DeflateImageAsync(string pdfFilename, int imageNumber, int pageNumber, int processRunId,  string extension, string serviceName)
    {
        var bytAry = await GetImageBytesAsync(
            new OcrServiceImageDataCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                Filename = pdfFilename,
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
            pdfFilename, 
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
        string pdfFilename,
        string extension,
        string serviceName,
        int? width = null,
        int? height = null)
    {
        return Task.FromResult(
            ImageReferenceHelper.GetImageReference(pageNumber, imageNumber, pdfFilename, extension));
    }
    
    public Task<List<ImageDetails>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetImagesAsync(request);
    }

    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetImageBytesAsync(request);
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetNoOcrPagesMetadataAsync(request);
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetNoOcrImagesMetadata(request);
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        return Task.FromResult(
            ImageReferenceHelper.GetNoOcrPageReferenceAsync(
                request.Filename!,
                request.NoOcrServiceName!,
                request.PageNumber));
    }
    
    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetNoOcrPageTextLinesAsync(request);
    }

    public Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetNoOcrAllPagesTextLinesAsync(request);
    }

    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetOcrScreenshotTextAsync(request);
    }
    
    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseReadService.GetOcrImageTextAsync(request);
    }
    
    public async Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);

        var content = await databaseReadService.GetTemporaryOcrImageTextAsync(request);
        return JsonSerializer.Deserialize<List<LineAndWords>>(content!, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);

        var content = await databaseReadService.GetTemporaryOcrScreenshotTextAsync(request);
        return JsonSerializer.Deserialize<List<LineAndWords>>(content!, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);

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
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
     
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
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return await databaseWriteService.SaveNoOcrPageAsync(request, pageLines, request.ProcessRunId);
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseWriteService.SaveOcrImageTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseWriteService.SaveOcrScreenshotTextAsync(request, pageLines, request.ProcessRunId);
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseWriteService.SaveOcrScreenshotTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseWriteService.SaveOcrImageTextAsync(request, pageLines, request.ProcessRunId);
    }
    
    public async Task<int> SaveImageOnPageAsync(byte[] bytes, int width, int height, string pdfFilename, string noOcrServiceName, int imageNumber, int pageNumber, string extension, int processRunId)
    {
        var filename = FileHelper.GetFilenameWithoutExtension(pdfFilename)!;
        await databaseWriteService.SaveImageOnPageAsync(bytes, width, height, filename, noOcrServiceName, imageNumber, pageNumber, extension, processRunId);

        return bytes.Length;
    }
    
    public Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseWriteService.SaveTemporaryOcrImageTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId); // TODO
    }
    
    public Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filename = FileHelper.GetFilenameWithoutExtension(request.Filename!);
        return databaseWriteService.SaveTemporaryOcrScreenshotTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }

    public Task<MetadataCollection?> GetMetadataAsync(
        string pdfFilename,
        string noOcrServiceName,
        int processRunId)
    {
        return BaseCacheService.GetMetadataAsync(
            this,
            pdfFilename,
            noOcrServiceName,
            processRunId);
    }

    public async Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync(int regionCode)
    {
        var data = await databaseReadService.GetNaldLinkedLicenceRawDataAsync();
 
        return data
            .Where(dataLine => dataLine.RegionCode == regionCode)
            .ToList();
    }

    public async Task<NaldDataCollection> GetNaldDataAsync(short? regionCode)
    {
        var licencesTask = databaseReadService.GetNaldAbsLicencesAsync(regionCode);
        var versionsTask = databaseReadService.GetNaldLicenceVersionsAsync(regionCode);
        var purposesTask = databaseReadService.GetNaldLicencePurposesAsync(regionCode);
        var pointsTask = databaseReadService.GetNaldLicencePointsAsync(regionCode);
        var quantitiesTask = databaseReadService.GetNaldLicenceQuantitiesAsync(regionCode);
        var licencesAlternateFormatTask = databaseReadService.GetNaldLicencesAsync();
        
        return new NaldDataCollection
        {
            Licences = await licencesTask,
            LicencesAlternateFormat = await licencesAlternateFormatTask,
            LicenceVersions = await versionsTask,
            LicencePurposes = await purposesTask,
            LicencePoints = await pointsTask,
            LicenceQuantities = await quantitiesTask
        };
    }

    public Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode)
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

    public Task<List<NaldAbstractionLicenceDataLine>> GetNaldAbsLicencesAsync(short regionCode)
    {
        return databaseReadService.GetNaldAbsLicencesAsync(regionCode);
    }

    public Task<List<NaldLicenceVersionDataLine>> GetNaldLicenceVersionsAsync(short regionCode)
    {
        return databaseReadService.GetNaldLicenceVersionsAsync(regionCode);
    }

    public Task<List<NaldLicencePurposeDataLine>> GetNaldLicencePurposesAsync(short regionCode)
    {
        return databaseReadService.GetNaldLicencePurposesAsync(regionCode);
    }

    public Task<List<NaldLicencePointDataLine>> GetNaldLicencePointsAsync(short regionCode)
    {
        return databaseReadService.GetNaldLicencePointsAsync(regionCode);
    }

    public Task<List<NaldLicenceQuantitiesDataLine>> GetNaldLicenceQuantitiesAsync(short regionCode)
    {
        return databaseReadService.GetNaldLicenceQuantitiesAsync(regionCode);
    }
}