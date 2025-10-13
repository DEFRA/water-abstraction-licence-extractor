using System.Text.Json;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Services;

public class FileSystemCacheService(string cacheFolder) : ICacheService
{
    public Task SetupAsync()
    {
        Directory.CreateDirectory(cacheFolder);
        return Task.CompletedTask;
    }
    
    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{txtCacheFolder}/{PositionConstants.CacheMetadataFilename}";
        var existsInCache = File.Exists(metadataFilename);

        if (!existsInCache)
        {
            return null;
        }
        
        return (string?)await File.ReadAllTextAsync(metadataFilename);
    }

    public async Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var imgCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Images";
        Directory.CreateDirectory(imgCacheFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{imgCacheFolder}/{PositionConstants.CacheMetadataFilename}";
        var existsInCache = File.Exists(metadataFilename);

        if (!existsInCache)
        {
            return null;
        }
        
        return (string?)await File.ReadAllTextAsync(metadataFilename);
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too
        
        return Task.FromResult($"{txtCacheFolder}/page-{request.PageNumber}.json");
    }
    
    public async Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        var outputFilename = await GetNoOcrPageReferenceAsync(request);
        var existsInCache = File.Exists(outputFilename);

        if (!existsInCache)
        {
            return null;
        }
        
        return await File.ReadAllTextAsync(outputFilename);
    }
    
    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, string pdfFilePath, string extension)
    {
        var fileCacheFolder= GetFolderPath(pdfFilePath);
        var outputFolderFull = $"{fileCacheFolder}/{PdfDataExtractorService.Name}/Images";
        Directory.CreateDirectory(outputFolderFull);

        var outputFilename = $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}.{extension}";
        return Task.FromResult(outputFilename);
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";

        if (!File.Exists(outputFilename))
        {
            return null;
        }
        
        return await File.ReadAllTextAsync(outputFilename);
    }

    public async Task<byte[]> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        var filePath = await GetImageReferenceAsync(
            request.PageNumber,
            request.ImageNumber,
            request.Filepath!,
            request.Extension!);
        
        return await File.ReadAllBytesAsync(filePath);
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{txtCacheFolder}/{PositionConstants.CacheMetadataFilename}";
        
        var data = new Dictionary<string, object>
        {
            { "pages", pagesMetadata },
            { "allTextFilename", "pages-all.txt" }
        };
        
        await File.WriteAllTextAsync(
            metadataFilename,
            JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions()));

        return request;
    }

    public Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        return File.WriteAllTextAsync(
            GetImageMetadataFilename(request.NoOcrServiceName!, GetFolderPath(request.Filepath!)),
            JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions()));
    }

    public async Task SaveImageAsync(byte[] bytes, string pdfFilePath, int imageNumber, int pageNumber, string extension)
    {
        var filePath = await GetImageReferenceAsync(pageNumber, imageNumber, pdfFilePath, extension);
        await File.WriteAllBytesAsync(filePath, bytes);
    }
    
    public async Task<byte[]> SaveDeflatedImageAsync(string pdfFilePath, int imageNumber, int pageNumber)
    {
        var bytAry = await GetImageBytesAsync(new OcrServiceImageDataCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilePath
        });
        
        var deflated = PdfPigNoOcrImageService.Deflate(bytAry);

        var fileCacheFolder= GetFolderPath(pdfFilePath);
        var outputFolderFull = $"{fileCacheFolder}/{PdfDataExtractorService.Name}/Images";
        var imagePath = $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}.jpg";
        
        var imageFilenameDeflated = imagePath.Replace(".jpg", "-deflated.jpg",
            StringComparison.InvariantCultureIgnoreCase);
        await File.WriteAllBytesAsync(imageFilenameDeflated, deflated);

        return deflated;
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too
        
        var outputFilename = $"{txtCacheFolder}/page-{request.PageNumber}.json";
        
        await File.WriteAllTextAsync(
            outputFilename,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
        
        return request;
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";
        
        return File.WriteAllTextAsync(
            outputFilename,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.Filepath!);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";
        return File.WriteAllTextAsync(outputFilename, pageLines);
    }

    private string GetFolderPath(string pdfFilePath)
    {
        var fileOutputFolder = Path.Combine(cacheFolder, FileHelper.GetFilenameWithoutExtension(pdfFilePath));
        if (fileOutputFolder.StartsWith('/'))
        {
            fileOutputFolder = fileOutputFolder[1..];
        }

        return fileOutputFolder.Trim();
    }
    
    private string GetImageMetadataFilename(string serviceName, string folderPath)
    {
        var imagesMetadataFolder = $"{folderPath}/{serviceName}/Images";
        Directory.CreateDirectory(imagesMetadataFolder); // This checks if exists, and creates the whole path too
        
        return $"{imagesMetadataFolder}/{PositionConstants.CacheMetadataFilename}";
    }
}