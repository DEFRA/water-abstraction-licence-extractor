using System.Text.Json;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Cache;

public class FileSystemCacheService(string cacheFolder) : ICacheService
{
    public bool UsesDatabase { get; set; } = false;

    public string? CacheFolderOrUrl { get; set; } = cacheFolder.StartsWith('/') ? cacheFolder : Path.GetFullPath(cacheFolder);

    public Task SetupAsync()
    {
        Directory.CreateDirectory(CacheFolderOrUrl!);
        return Task.CompletedTask;
    }

    public Task ClearCacheAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task ClearCacheAsync()
    {
        throw new NotImplementedException();
    }
    
    public async Task<byte[]> DeflateImageAsync(Guid fileId, int imageNumber, int pageNumber, int processRunId, string extension, string serviceName)
    {
        var bytAry = await GetImageBytesAsync(new OcrServiceImageDataCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            FileId = fileId,
            ProcessRunId = processRunId,
            Extension = extension,
            NoOcrServiceName = serviceName
        });

        if (bytAry == null)
        {
            throw new Exception("Image could not be found");
        }
        
        var deflated = ImageHelper.Deflate(bytAry);

        var fileCacheFolder= GetFolderPath(fileId);
        var outputFolderFull = $"{fileCacheFolder}/{serviceName}/Images";
        var imagePath = $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}.jpg";
        
        var imageFilenameDeflated = imagePath.Replace(".jpg", "-deflated.jpg",
            StringComparison.InvariantCultureIgnoreCase);
        await File.WriteAllBytesAsync(imageFilenameDeflated, deflated);

        return deflated;
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
        var fileCacheFolder= GetFolderPath(fileId);
        var outputFolderFull = $"{fileCacheFolder}/{serviceName}/Images";
        Directory.CreateDirectory(outputFolderFull);

        var outputFilename = width.HasValue ?
            $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}+{width}+{height}.{extension}"
            : $"{outputFolderFull}/page-{pageNumber}-image-{imageNumber}.{extension}";
        
        return Task.FromResult(outputFilename);
    }
    
    public Task<List<ImageDetails>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        // NOTE - This doesn't take into account any of the filters except Filename and NoOcrServiceName
        
        var fileCacheFolder= GetFolderPath(request.FileId);
        var imgCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Images";

        var files = Directory.GetFiles(imgCacheFolder).Select(f => f.Split('/').Last()).ToList();
        files = files.Where(f => f.StartsWith("page-") && f.Contains("-image-")).ToList();
        
        var returnList = new List<ImageDetails>();

        foreach (var filename in files)
        {
            var extensionParts = filename.Split('.');
            var extension = extensionParts[1];
            
            var topLevelParts = extensionParts[0].Split('+');
            var width = topLevelParts.Length >= 3 ? Convert.ToInt32(topLevelParts[1]) : throw new Exception("Filename doesnt have width component");
            var height = topLevelParts.Length >= 3 ? Convert.ToInt32(topLevelParts[2]) : throw new Exception("Filename doesnt have height component");
            
            var parts = topLevelParts[0].Split("-image-");
            var pageNumber = int.Parse(parts[0].Replace("page-", string.Empty));
            
            var imageNumber = int.Parse(parts[1]);

            returnList.Add(new ImageDetails
            {
                pageNumber = pageNumber,
                imageNumber = imageNumber,
                extension = extension,
                width = width,
                height = height
            });
        }

        return Task.FromResult(returnList);
    }

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        if (request.PageNumber == null)
        {
            throw new ArgumentNullException(nameof(request.PageNumber));
        }
        
        if (request.ImageNumber == null)
        {
            throw new ArgumentNullException(nameof(request.ImageNumber));
        }
        
        var filePath = await GetImageReferenceAsync(
            request.PageNumber!.Value,
            request.ImageNumber!.Value,
            request.FileId,
            request.Extension!,
            request.NoOcrServiceName!);

        if (!filePath.StartsWith('/'))
        {
            filePath = $"/{filePath}";
        }

        var fileNameWithoutExtension = filePath.Replace($".{request.Extension}", string.Empty).Split('/').Last(); // TODO use helper
        var directory = filePath[..^filePath.Split('/').Last().Length];

        var files = Directory
            .GetFiles(directory)
            .Select(x => x.Split('/').Last())
            .ToList();
        
        var matchingFile = files
            .FirstOrDefault(x => x.StartsWith($"{fileNameWithoutExtension}+") && x.EndsWith($".{request.Extension}"));

        if (matchingFile == null)
        {
            throw new Exception($"No file found for {fileNameWithoutExtension} and extension {request.Extension} in {directory} (Filepath was {filePath}");
        }
        
        return await File.ReadAllBytesAsync($"{directory}/{matchingFile}");
    }
    
    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too
        
        return Task.FromResult($"{txtCacheFolder}/page-{request.PageNumber}.json");
    }

    public async Task<string?> GetNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
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

    public async Task<Dictionary<int, string>?> GetNoOcrAllPagesTextLinesAsync(NoOcrServiceMetadataCacheRequest request)
    {
        var returnDictionary = new Dictionary<int, string>();

        var metadataFileText = await GetNoOcrPagesMetadataAsync(
            new NoOcrServiceMetadataCacheRequest
            {
                FileId = request.FileId,
                NoOcrServiceName = request.NoOcrServiceName,
                ProcessRunId = request.ProcessRunId
            });

        if (string.IsNullOrEmpty(metadataFileText))
        {
            return null;
        }
        
        var pagesTextMetadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
            metadataFileText!,
            JsonHelper.GetSerializerOptions())!;

        var pageArray = ((JsonElement)pagesTextMetadata["pages"]).EnumerateArray().ToList();
        
        for (var pageNumber = 1; pageNumber <= pageArray.Count; pageNumber++)
        {
            var pageRequest = new NoOcrServicePageCacheRequest
            {
                PageNumber = pageNumber,
                NoOcrServiceName = request.NoOcrServiceName,
                ProcessRunId = request.ProcessRunId,
                FileId = request.FileId,
            };
            
            var outputFilename = await GetNoOcrPageReferenceAsync(pageRequest);
            var existsInCache = File.Exists(outputFilename);

            if (!existsInCache)
            {
                continue;
            }
            
            returnDictionary.Add(pageNumber, await File.ReadAllTextAsync(outputFilename));
        }
        
        return returnDictionary;
    }

    public async Task<string?> GetNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
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
    
    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";

        if (!File.Exists(outputFilename))
        {
            return null;
        }
        
        return await File.ReadAllTextAsync(outputFilename);
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}.json";

        if (!File.Exists(outputFilename))
        {
            return null;
        }
        
        return await File.ReadAllTextAsync(outputFilename);
    }

    public async Task<List<LineAndWords>> GetAndSaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/temporary-ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";

        if (!File.Exists(outputFilename))
        {
            return [];
        }
        
        var content = await File.ReadAllTextAsync(outputFilename);

        try
        {
            return JsonSerializer.Deserialize<List<LineAndWords>>(content, JsonHelper.GetSerializerOptions())!;
        }
        catch
        {
            ConsoleHelper.WriteLine($"MALFORMED JSON ERROR - {content}");
            throw;
        }
    }
    
    public async Task<List<LineAndWords>> GetAndSaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/temporary-ocr-page-{request.PageNumber}.json";

        if (!File.Exists(outputFilename))
        {
            return [];
        }
        
        var content = await File.ReadAllTextAsync(outputFilename);
        return JsonSerializer.Deserialize<List<LineAndWords>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<int> SaveImageOnPageAsync(byte[] bytes, int width, int height, Guid fileId, string noOcrServiceName, int imageNumber, int pageNumber, string extension, int processRunId)
    {
        var filePath = await GetImageReferenceAsync(pageNumber, imageNumber, fileId, extension, noOcrServiceName, width, height);
        await File.WriteAllBytesAsync(filePath, bytes);

        return bytes.Length;
    }
    
    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
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

    public Task SaveNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        return File.WriteAllTextAsync(
            GetImageMetadataFilename(request.NoOcrServiceName!, GetFolderPath(request.FileId)),
            JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions()));
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLinesAsync(
        NoOcrServicePageCacheRequest request,
        string pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var txtCacheFolder = $"{fileCacheFolder.Replace("//", "/")}/{request.NoOcrServiceName}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too
        
        var outputFilename = $"{txtCacheFolder}/page-{request.PageNumber}.json";

        await File.WriteAllTextAsync(
            outputFilename,
            pageLines);
        
        return request;
    }

    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";
        
        return File.WriteAllTextAsync(
            outputFilename,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}.json";
        return File.WriteAllTextAsync(outputFilename, pageLines);
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}.json";
        
        return File.WriteAllTextAsync(
            outputFilename,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
    }
    
    public Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";
        return File.WriteAllTextAsync(outputFilename, pageLines);
    }

    public Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/temporary-ocr-page-{request.PageNumber}-image-{request.ImageNumber}.json";
        ConsoleHelper.WriteLine($"Writing to {fileCacheFolder}");
        
        return File.WriteAllTextAsync(
            outputFilename,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
    }
    
    public Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var fileCacheFolder= GetFolderPath(request.FileId);
        var folder = $"{fileCacheFolder}/{request.OcrServiceName}/Text";
        Directory.CreateDirectory(folder);
    
        var outputFilename = $"{folder}/temporary-ocr-page-{request.PageNumber}.json";
        
        return File.WriteAllTextAsync(
            outputFilename,
            JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
    }

    public Task<MetadataCollection?> GetMetadataAsync(Guid fileId, string noOcrServiceName, int processRunId)
    {
        return BaseCacheService.GetMetadataAsync(
            this,
            fileId,
            noOcrServiceName,
            processRunId);
    }

    public Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync()
    {
        throw new NotImplementedException();
    }

    public Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take)
    {
        throw new NotImplementedException();
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
        HashSet<(string, int)> Impoundment)> GetNaldLicenceNumbersAsync(short? regionCode)
    {
        throw new NotImplementedException();
    }

    public Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync()
    {
        throw new NotImplementedException();
    }

    public Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation)
    {
        return Task.CompletedTask;
    }

    public Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber)
    {
        throw new NotImplementedException();
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

    public Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take)
    {
        throw new NotImplementedException();
    }

    public Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results)
    {
        throw new NotImplementedException();
    }

    public Task ClearLicenceFinderResultsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync()
    {
        throw new NotImplementedException();
    }

    public Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results)
    {
        throw new NotImplementedException();
    }

    public Task<List<VersionFile>> GetVersionFilesAsync()
    {
        throw new NotImplementedException();
    }

    public Task SaveVersionFilesAsync(List<VersionFile> results)
    {
        throw new NotImplementedException();
    }

    public Task ClearVersionFilesAsync()
    {
        throw new NotImplementedException();
    }

    public Task ClearVersionFilesToDownloadAsync()
    {
        throw new NotImplementedException();
    }

    private string GetFolderPath(Guid fileId)
    {
        var fileOutputFolder = Path.Combine(CacheFolderOrUrl!, fileId.ToString());
        return fileOutputFolder.Trim();
    }
    
    private string GetImageMetadataFilename(string serviceName, string folderPath)
    {
        var imagesMetadataFolder = $"{folderPath}/{serviceName}/Images";
        Directory.CreateDirectory(imagesMetadataFolder); // This checks if exists, and creates the whole path too
        
        return $"{imagesMetadataFolder}/{PositionConstants.CacheMetadataFilename}";
    }
}