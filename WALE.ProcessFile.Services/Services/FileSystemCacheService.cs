using System.Text.Json;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;

namespace WALE.ProcessFile.Services.Services;

public class FileSystemCacheService(string cacheFolder) : ICacheService
{
    public Task SetupAsync()
    {
        Directory.CreateDirectory(cacheFolder);
        return Task.CompletedTask;
    }
    
    public async Task<string?> GetNoOcrMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(cacheFolder, request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{txtCacheFolder}/{PositionConstants.CacheMetadataFilename}";
        var existsInCache = File.Exists(metadataFilename);

        if (!existsInCache)
        {
            return null;
        }
        
        return (string?)await File.ReadAllTextAsync(metadataFilename);
    }

    public async Task<string?> GetNoOcrPageAsync(NoOcrServicePageCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(cacheFolder, request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too
        
        var outputFilename = $"{txtCacheFolder}/page-{request.PageNumber}.json";
        return await File.ReadAllTextAsync(outputFilename);
    }
    
    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrMetadata(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        var fileCacheFolder= GetFolderPath(cacheFolder, request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.OcrServiceName}/Text";
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

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPage(
        NoOcrServicePageCacheRequest request,
        List<TextBlock> pageLines)
    {
        var fileCacheFolder= GetFolderPath(cacheFolder, request.Filepath!);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too
        
        var outputFilename = $"{txtCacheFolder}/page-{request.PageNumber}.json";
        
        await File.WriteAllTextAsync(
            outputFilename,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
        
        return request;
    }
    
    private static string GetFolderPath(string outputFolder, string pdfFilePath)
    {
        try
        {
            var fileOutputFolder = Path.Combine(outputFolder, FileHelper.GetFilenameWithoutExtension(pdfFilePath));
            if (fileOutputFolder.StartsWith('/'))
            {
                fileOutputFolder = fileOutputFolder[1..];
            }

            return fileOutputFolder.Trim();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
}