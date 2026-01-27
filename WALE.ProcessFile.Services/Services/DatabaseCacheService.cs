using System.Text.Json;
using Tesseract;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Services;

public class DatabaseCacheService(
    IDatabaseReadService databaseReadService,
    IDatabaseWriteService databaseWriteService,
    string host,
    int port,
    string databaseName,
    string username,
    string password) : ICacheService
{
    public bool UsesDatabase { get; set; } = true;

    public string? CacheFolder { get; set; } = null;

    public string? Host { get; set; } = host;
    
    public int Port { get; set; } = port;
    
    public string? DatabaseName { get; set; } = databaseName;
    
    public string? Username { get; set; } = username;
    
    public string? Password { get; set; } = password;

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
    
    public async Task<byte[]> DeflateImageAsync(string pdfFilePath, int imageNumber, int pageNumber, int processRunId,  string extension)
    {
        var bytAry = await GetImageBytesAsync(new OcrServiceImageDataCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilePath,
            Extension = extension
        });

        if (bytAry == null)
        {
            throw new Exception("Image could not be found");
        }
        
        var deflatedBytes = PdfPigNoOcrImageService.Deflate(bytAry);
        var pix = Pix.LoadFromMemory(deflatedBytes);
        
        await databaseWriteService.SaveImageOnPageAsync(
            deflatedBytes,
            pix.Width,
            pix.Height,
            pdfFilePath, 
            PdfDataExtractorService.Name,
            imageNumber,
            pageNumber,
            "jpg",
            processRunId);
        
        return deflatedBytes;
    }
    
    public Task<string> GetImageReferenceAsync(
        int pageNumber,
        int imageNumber,
        string pdfFilePath,
        string extension,
        int? width = null,
        int? height = null)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        return Task.FromResult($"ImageReference-{pdfFilename}-{extension}-{pageNumber}-{imageNumber}");
    }
    
    public Task<List<(int pageNumber, int imageNumber, string extension, int width, int height)>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetImagesAsync(request);
    }

    public Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetImageBytesAsync(request);
    }

    public Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetNoOcrPagesMetadataAsync(request);
    }

    public Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetNoOcrImagesMetadata(request);
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        var pdfFilename = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return Task.FromResult($"NoOcrPageReference-{pdfFilename}-{request.NoOcrServiceName}-{request.PageNumber}");
    }
    
    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetNoOcrPageTextLinesAsync(request);
    }

    public Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetOcrScreenshotTextAsync(request);
    }
    
    public Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseReadService.GetOcrImageTextAsync(request);
    }
    
    public async Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);

        var content = await databaseReadService.GetTemporaryOcrImageTextAsync(request);
        return JsonSerializer.Deserialize<List<LineAndWords>>(content!, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);

        var content = await databaseReadService.GetTemporaryOcrScreenshotTextAsync(request);
        return JsonSerializer.Deserialize<List<LineAndWords>>(content!, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);

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

    public async Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
     
        var existing = await databaseReadService.GetNoOcrImagesMetadata(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return;
        }
        
        var imagesMetadataStr = JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions());
        await databaseWriteService.SaveNoOcrImagesMetadata(request, imagesMetadataStr, request.ProcessRunId);
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);

        var existing = await databaseReadService.GetNoOcrPageTextLinesAsync(request);

        if (!string.IsNullOrEmpty(existing))
        {
            return request;
        }
        
        var pageLinesStr = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions());
        return await databaseWriteService.SaveNoOcrPageAsync(request, pageLinesStr, request.ProcessRunId);
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseWriteService.SaveOcrImageTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseWriteService.SaveOcrScreenshotTextAsync(request, pageLines, request.ProcessRunId);
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseWriteService.SaveOcrScreenshotTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseWriteService.SaveOcrImageTextAsync(request, pageLines, request.ProcessRunId);
    }
    
    public Task SaveImageOnPageAsync(byte[] bytes, int width, int height, string pdfFilePath, string noOcrServiceName, int imageNumber, int pageNumber, string extension, int processRunId)
    {
        var filename = FileHelper.GetFilenameWithoutExtension(pdfFilePath)!;
        return databaseWriteService.SaveImageOnPageAsync(bytes, width, height, filename, noOcrServiceName, imageNumber, pageNumber, extension, processRunId);
    }
    
    public Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseWriteService.SaveTemporaryOcrImageTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId); // TODO
    }
    
    public Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        request.Filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath!);
        return databaseWriteService.SaveTemporaryOcrScreenshotTextAsync(request, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()), request.ProcessRunId);
    }
}