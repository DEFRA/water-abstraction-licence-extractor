using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Core.Models.NoOcrService;
using WALE.ProcessFile.Core.Models.OcrService;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Cache;

public class DatabaseCacheService(
    IDatabaseReadService databaseReadService,
    IDatabaseWriteService databaseWriteService,
    IImageService imageService) : ICacheService
{
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

        // Real bytes go to S3; the Postgres row carries only the metadata GetImagesAsync needs
        // (width/height/page/image/extension) - see SaveImageOnPageAsync below for why.
        var s3Key = ImageReferenceHelper.GetImageOnPageS3Key(fileId, serviceName, pageNumber, imageNumber, "jpg");
        await imageService.UploadAsync(s3Key, new MemoryStream(deflatedBytes), "image/jpeg");

        await databaseWriteService.SaveImageOnPageMetadataAsync(
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

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        // S3 first (all new writes land there - see SaveImageOnPageAsync below), falling back to
        // Postgres for rows written before this table moved to S3 and not yet migrated by the
        // Tools backfill.
        var s3Key = ImageReferenceHelper.GetImageOnPageS3Key(
            request.FileId,
            request.NoOcrServiceName!,
            request.PageNumber!.Value,
            request.ImageNumber!.Value,
            request.Extension!);

        return await imageService.DownloadAsync(s3Key)
            ?? await databaseReadService.GetImageBytesAsync(request);
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
    
    public async Task<List<LineAndWords>> GetAndSaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var text = await databaseReadService.GetTemporaryOcrImageTextAsync(request);

        if (string.IsNullOrWhiteSpace(text))
        {
            throw new Exception("No temporary OCR image text found");
        }
        
        var linesAndWords =  JsonSerializer.Deserialize<List<LineAndWords>>(text, JsonHelper.GetSerializerOptions())!;
        
        await databaseWriteService.SaveOcrImageTextAsync(
            request,
            text,
            request.ProcessRunId);

        await databaseWriteService.DeleteTemporaryOcrImageTextAsync(request);
        return linesAndWords;
    }

    public async Task<List<LineAndWords>> GetAndSaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var text = await databaseReadService.GetTemporaryOcrScreenshotTextAsync(request);
        
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new Exception("No temporary OCR screenshot text found");
        }
        
        var linesAndWords = JsonSerializer.Deserialize<List<LineAndWords>>(text, JsonHelper.GetSerializerOptions())!;

        await databaseWriteService.SaveOcrScreenshotTextAsync(
            request,
            text,
            request.ProcessRunId);
        
        await databaseWriteService.DeleteTemporaryOcrScreenshotTextAsync(request);
        return linesAndWords;
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
        // Real bytes go to S3 only (image_on_page.data is NotNullable, so the Postgres row still
        // exists - GetImagesAsync's metadata listing needs page/image/width/height/extension - but
        // carries an empty placeholder instead of the real bytes; GetImageBytesAsync above checks
        // S3 first so that placeholder is never actually served for a row written this way).
        // Upload before the metadata insert so a failed upload doesn't leave a phantom row with no
        // recoverable bytes anywhere.
        var s3Key = ImageReferenceHelper.GetImageOnPageS3Key(fileId, noOcrServiceName, pageNumber, imageNumber, extension);
        var contentType = extension.Equals("png", StringComparison.OrdinalIgnoreCase) ? "image/png" : "image/jpeg";
        await imageService.UploadAsync(s3Key, new MemoryStream(bytes), contentType);

        await databaseWriteService.SaveImageOnPageMetadataAsync(
            width, height, fileId, noOcrServiceName, imageNumber, pageNumber, extension, processRunId);

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
    
    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync()
    {
        return databaseReadService.GetDmsFileIdInformationAsync();
    }

    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync(Guid fileId)
    {
        return databaseReadService.GetDmsFileIdInformationAsync(fileId);
    }

    public Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation)
    {
        return databaseWriteService.AddDmsFileIdInformationAsync(newDmsFileIdInformation);
    }

    public Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult)
    {
        return databaseWriteService.SaveDmsFileReaderResultAsync(dmsFileReaderResult);
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
    
    public Task<HashSet<string>> GetFirstNamesAsync()
    {
        throw new NotImplementedException();
    }

    public Task<DmsFileData?> GetDmsFileDataAsync(string? licenceNumber)
    {
        return databaseReadService.GetDmsFileDataAsync(licenceNumber);
    }
}