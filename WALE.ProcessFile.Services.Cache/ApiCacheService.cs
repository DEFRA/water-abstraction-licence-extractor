using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Core.Models.PdfPig;

namespace WALE.ProcessFile.Services.Cache;

public class ApiCacheService(HttpClient httpClient) : ICacheService
{
    public bool UsesDatabase { get; set; } = true;
    public string? CacheFolderOrUrl { get; set; } = httpClient.BaseAddress?.ToString();
    
    public Task SetupAsync()
    {
        throw new NotImplementedException();
    }

    public Task ClearCacheAsync(string pdfFilename)
    {
        throw new NotImplementedException();
    }

    public Task ClearCacheAsync()
    {
        throw new NotImplementedException();
    }

    public Task<byte[]> DeflateImageAsync(string pdfFilePath, int imageNumber, int pageNumber, int processRunId, string extension,
        string serviceName)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, string pdfFilePath, string extension, string serviceName,
        int? width = null, int? height = null)
    {
        throw new NotImplementedException();
    }

    public async Task<byte[]?> GetImageBytesAsync(OcrServiceImageDataCacheRequest request)
    {
        var path = $"/Extractor/Images/GetImage?pageNumber={request.PageNumber}"
           + $"&imageNumber={request.ImageNumber}&filename={request.Filepath}"
           + $"&noOcrServiceName={request.NoOcrServiceName}&extension={request.Extension}";
        
        var response = await httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    public async Task<List<ImageDetails>>
        GetImagesAsync(OcrServiceImageDataCacheRequest request)
    {
        var path = $"/Extractor/Images/GetAll?filename={request.Filepath}&noOcrServiceName={request.NoOcrServiceName}";
        
        var response = await httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<ImageDetails>>(
            content,
            JsonHelper.GetSerializerOptions())!;
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

    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath);
        var path = $"/Extractor/Images/GetImageText?pageNumber={request.PageNumber}"
            + $"&imageNumber={request.ImageNumber}&filepath={filepath}"
            + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath);
        var path = $"/Extractor/Images/GetScreenshotText?pageNumber={request.PageNumber}"
           + $"&imageNumber={request.ImageNumber}&filepath={filepath}"
           + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }

    public Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public Task SaveImageOnPageAsync(byte[] bytes, int width, int height, string pdfFilePath, string noOcrServiceName,
        int imageNumber, int pageNumber, string extension, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, List<Dictionary<string, object>> pagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task SaveNoOcrImagesMetadata(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        throw new NotImplementedException();
    }

    public Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLines(NoOcrServicePageCacheRequest request, List<MinimalTextBlock> pageLines)
    {
        throw new NotImplementedException();
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var path = "/Extractor/Images/SaveOcrImageTextRaw";

        var json = JsonSerializer.Serialize(new
        {
            Request = request,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Images/SaveOcrImageText";

        var json = JsonSerializer.Serialize(new
        {
            Request = request,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var path = "/Extractor/Images/SaveOcrScreenshotText";

        var json = JsonSerializer.Serialize(new
        {
            Request = request,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        throw new NotImplementedException();
    }

    public async Task SaveTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Images/SaveTemporaryOcrImageText";

        var json = JsonSerializer.Serialize(new
        {
            Request = request,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Images/SaveTemporaryOcrScreenshotText";

        var json = JsonSerializer.Serialize(new
        {
            Request = request,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task<MetadataCollection?> GetMetadataAsync(string pdfFilePath, string noOcrServiceName, int processRunId)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(pdfFilePath);
        var path = $"/Extractor/Metadata/Get?filename={filepath}&noOcrServiceName={noOcrServiceName}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return !string.IsNullOrEmpty(content)
            ? JsonSerializer.Deserialize<MetadataCollection?>(content, JsonHelper.GetSerializerOptions())
            : null;
    }

    public async Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync(int regionCode)
    {
        var path = $"/Extractor/LinkedLicence/GetMap?regionCode={regionCode}";
        
        var response = await httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<NaldLinkedLicenceRawData>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NaldDataCollection> GetNaldDataAsync(short regionCode)
    {
        var path = $"/Extractor/NaldData/GetAll?regionCode={regionCode}";
        
        var response = await httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldDataCollection>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short regionCode)
    {
        var path = $"/Extractor/NaldData/GetLicenceStatusData?regionCode={regionCode}";
        
        var response = await httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldLicenceStatusData>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public Task<(HashSet<string> Live, HashSet<string> Dead, HashSet<string> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode)
    {
        throw new NotImplementedException();
    }
}