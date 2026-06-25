using System.Net.Http.Headers;
using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Cache;

public class ApiCacheService(HttpClient httpClient) : ICacheService
{
    public bool UsesDatabase { get; set; } = true; // Because its back by a DB
    public string? CacheFolderOrUrl { get; set; } = httpClient.BaseAddress?.ToString();
    
    public Task SetupAsync()
    {
        return Task.CompletedTask;
    }

    public async Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync()
    {
        var path = "/Extractor/LicenceFinder/GetResults";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<LicenceFinderResult>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }
    
    public async Task ClearCacheAsync(Guid fileId)
    {
        var path = $"/Extractor/Cache/ClearSingle?fileId={fileId}";
       
        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearCacheAsync()
    {
        var path = "/Extractor/Cache/ClearAll";
       
        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DeflateImageAsync(
        Guid fileId,
        int imageNumber,
        int pageNumber,
        int processRunId,
        string extension,
        string serviceName)
    {
        var path = $"/Extractor/Images/DeflateImage?fileId={fileId}"
           + $"&imageNumber={imageNumber}&pageNumber={pageNumber}"
           + $"&processRunId={processRunId}&extension={extension}&serviceName={serviceName}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, Guid fileId, string extension, string serviceName,
        int? width = null, int? height = null)
    {
        return Task.FromResult(
            ImageReferenceHelper.GetImageReference(pageNumber, imageNumber, fileId, extension));
    }

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        var path = $"/Extractor/Images/GetImage?pageNumber={request.PageNumber}"
           + $"&imageNumber={request.ImageNumber}&fileId={request.FileId}"
           + $"&noOcrServiceName={request.NoOcrServiceName}&extension={request.Extension}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<List<ImageDetails>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        var path = $"/Extractor/Images/GetAll?fileId={request.FileId}&noOcrServiceName={request.NoOcrServiceName}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ImageDetails>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public Task<string> GetNoOcrPageReferenceAsync(NoOcrServicePageCacheRequest request)
    {
        return Task.FromResult(
            ImageReferenceHelper.GetNoOcrPageReferenceAsync(
                request.FileId!,
                request.NoOcrServiceName!,
                request.PageNumber));
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


    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var path = $"/Extractor/Ocr/GetImageText?pageNumber={request.PageNumber}"
            + $"&imageNumber={request.ImageNumber}&fileId={request.FileId}"
            + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        return content;
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var path = $"/Extractor/Ocr/GetScreenshotText?pageNumber={request.PageNumber}"
           + $"&imageNumber={request.ImageNumber}&fileId={request.FileId}"
           + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }

    public async Task<List<LineAndWords>> GetAndSaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var path = $"/Extractor/Ocr/GetAndSaveTemporaryImageText?pageNumber={request.PageNumber}"
            + $"&imageNumber={request.ImageNumber}&fileId={request.FileId}"
            + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<List<LineAndWords>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<List<LineAndWords>> GetAndSaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var path = $"/Extractor/Ocr/GetAndSaveTemporaryScreenshotText?pageNumber={request.PageNumber}"
            + $"&fileId={request.FileId}"
            + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        return JsonSerializer.Deserialize<List<LineAndWords>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<int> SaveImageOnPageAsync(
        byte[] bytes,
        int width,
        int height,
        Guid fileId,
        string noOcrServiceName,
        int imageNumber,
        int pageNumber,
        string extension,
        int processRunId)
    {
        //var dtStart = DateTime.UtcNow;
        var path = $"/Extractor/Images/SaveImageOnPage?width={width}&height={height}&fileId={fileId}" +
            $"&noOcrServiceName={noOcrServiceName}&imageNumber={imageNumber}&pageNumber={pageNumber}" +
            $"&extension={extension}&processRunId={processRunId}";

        var contentType = extension.Equals("png", StringComparison.InvariantCultureIgnoreCase)
            ? "image/png"
            : "image/jpeg";
        
        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(bytes, 0, bytes.Length);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        form.Add(imageContent, "image", $"image.{extension}");

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), form));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        //var tsDuration = (DateTime.UtcNow - dtStart).TotalMilliseconds.ToString("0.0");
        //ConsoleHelper.WriteLine(
        //    $"SaveImageOnPageAsync API call (P{pageNumber}, {noOcrServiceName}) took {tsDuration}ms");

        return int.Parse(content);
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        var path = "/Extractor/NoOcr/SaveNoOcrPagesMetadata";
        var pagesMetadataJson = JsonSerializer.Serialize(pagesMetadata, JsonHelper.GetSerializerOptions());
        
        var json = JsonSerializer.Serialize(new
        {
            request.FileId,
            request.NoOcrServiceName,
            request.ProcessRunId,
            pageLines = pagesMetadataJson
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        return request;
    }

    public async Task SaveNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        var path = "/Extractor/NoOcr/SaveNoOcrImagesMetadata";

        var json = JsonSerializer.Serialize(new
        {
            request.FileId,
            request.NoOcrServiceName,
            request.ProcessRunId,
            ImagesMetadata = JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLinesAsync(
        NoOcrServicePageCacheRequest request,
        string pageLines)
    {
        var path = "/Extractor/NoOcr/SaveNoOcrPageTextLines";

        var json = JsonSerializer.Serialize(new
        {
            request.FileId,
            request.PageNumber,
            request.NoOcrServiceName,
            request.ProcessRunId,
            pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        return request;
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrImageText";

        var json = JsonSerializer.Serialize(new
        {
            request.FileId,
            request.OcrServiceName,
            request.ProcessRunId,
            request.PageNumber,
            request.ImageNumber,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrImageText";

        var json = JsonSerializer.Serialize(new
        {
            request.FileId,
            request.OcrServiceName,
            request.ProcessRunId,
            request.PageNumber,
            request.ImageNumber,
            PageLines = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrScreenshotText";
        
        var json = JsonSerializer.Serialize(new
        {
            request.FileId,
            request.PageNumber,
            request.ImageNumber,
            request.OcrServiceName,
            request.ProcessRunId,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrScreenshotText";

        var json = JsonSerializer.Serialize(new
        {
            request.FileId,
            request.PageNumber,
            request.ImageNumber,
            request.OcrServiceName,
            request.ProcessRunId,
            PageLines = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveTemporaryOcrImageTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Ocr/SaveTemporaryOcrImageText";

        var json = JsonSerializer.Serialize(new
        {
            request.PageNumber,
            request.ImageNumber,
            request.FileId,
            request.OcrServiceName,
            request.ProcessRunId,
            Text = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveTemporaryOcrScreenshotTextAsync(
        OcrServiceImageTextCacheRequest request,
        List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Ocr/SaveTemporaryOcrScreenshotText";

        var json = JsonSerializer.Serialize(new
        {
            request.PageNumber,
            request.FileId,
            request.OcrServiceName,
            request.ProcessRunId,
            Text = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<MetadataCollection?> GetMetadataAsync(Guid fileId, string noOcrServiceName, int processRunId)
    {
        var path = $"/Extractor/Metadata/Get?fileId={fileId}&noOcrServiceName={noOcrServiceName}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();

        var metadata = !string.IsNullOrEmpty(content)
            ? JsonSerializer.Deserialize<MetadataCollection?>(content, JsonHelper.GetSerializerOptions())
            : null;

        if (metadata != null)
        {
            metadata.SizeBytes = content.Length;
        }
        
        return metadata;
    }

    public async Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync()
    {
        var path = "/Extractor/LinkedLicence/GetMap";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<NaldLinkedLicenceRawData>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take)
    {
        var path = $"/Extractor/NaldData/GetAll?skip={skip}&take={take}";

        if (regionCode != null)
        {
            path += $"&regionCode={regionCode}";
        }
        
        if (allVersions)
        {
            path += "&allVersions=true";
        }

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldDataCollection>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode = null)
    {
        var path = "/Extractor/NaldData/GetLicenceStatusData";
        
        if (regionCode != null)
        {
            path += $"?regionCode={regionCode}";
        }
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldLicenceStatusData>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public Task<(
            HashSet<(string, int)> Live,
            HashSet<(string, int)> Lapsed,
            HashSet<(string, int)> Expired,
            HashSet<(string, int)> Revoked,
            HashSet<(string, int)> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode)
    {
        throw new NotImplementedException();
    }

    public async Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync()
    {
        var path = "/Extractor/Dms/GetFileIds";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<DmsFileIdInformation>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation)
    {
        var path = "/Extractor/Dms/AddFileIdInformation";
        var json = JsonSerializer.Serialize(newDmsFileIdInformation, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber)
    {
        var path = $"/Extractor/NaldData/GetCurrentIncrementNumber?permitNumber={permitNumber}" +
            $"&issueNumber={issueNumber}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return int.Parse(content);
    }

    public async Task<List<DmsExtract>> GetDmsExtractAsync(int skip, int take)
    {
        var path = $"/Extractor/Dms/GetExtract?skip={skip}&take={take}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<DmsExtract>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveDmsFileReaderResultAsync(DmsFileReaderResult dmsFileReaderResult)
    {
        var path = "/Extractor/Dms/SaveDmsFileReaderResult";
        var json = JsonSerializer.Serialize(dmsFileReaderResult, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync()
    {
        var path = "/Extractor/Dms/GetDmsFileReaderResults";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<DmsFileReaderResult>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveImportRunDateAsync(string dataSource)
    {
        var path = "/Extractor/Import/SaveDate";
        var json = JsonSerializer.Serialize(new
        {
            DataSource = dataSource
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<string?> GetImportRunDateAsync(string dataSource)
    {
        var path = "/Extractor/Import/GetDate";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }

    public async Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take)
    {
        var path = $"/Extractor/LicenceFinder/GetResults?skip={skip}&take={take}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<LicenceFinderResult>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results)
    {
        var path = "/Extractor/LicenceFinder/SaveResults";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearLicenceFinderResultsAsync()
    {
        var path = "/Extractor/LicenceFinder/ClearResults";
        
        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync()
    {
        var path = "/Extractor/VersionFiles/GetToDownload";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<VersionFileToDownload>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results)
    {
        var path = "/Extractor/VersionFiles/SaveToDownload";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<VersionFile>> GetVersionFilesAsync()
    {
        var path = "/Extractor/VersionFiles/GetAll";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<VersionFile>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveVersionFilesAsync(List<VersionFile> results)
    {
        var path = "/Extractor/VersionFiles/SaveAll";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearVersionFilesAsync()
    {
        var path = "/Extractor/VersionFiles/ClearAllFiles";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.DeleteAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearVersionFilesToDownloadAsync()
    {
        var path = "/Extractor/VersionFiles/ClearDownloadFiles";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.DeleteAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
    }
}