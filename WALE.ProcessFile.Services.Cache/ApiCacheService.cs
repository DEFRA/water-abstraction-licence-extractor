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

    public async Task ClearCacheAsync(string pdfFilename)
    {
        var path = $"/Extractor/Cache/ClearSingle?pdfFilePath={pdfFilename}";
       
        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearCacheAsync()
    {
        var path = "/Extractor/Cache/ClearAll";
       
        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task<byte[]> DeflateImageAsync(
        string pdfFilePath,
        int imageNumber,
        int pageNumber,
        int processRunId,
        string extension,
        string serviceName)
    {
        var path = $"/Extractor/Images/DeflateImage?pdfFilePath={pdfFilePath}"
           + $"&imageNumber={imageNumber}&pageNumber={pageNumber}"
           + $"&processRunId={processRunId}&extension={extension}&serviceName={serviceName}";
        
        var response = await httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsByteArrayAsync();
    }

    public Task<string> GetImageReferenceAsync(int pageNumber, int imageNumber, string pdfFilePath, string extension, string serviceName,
        int? width = null, int? height = null)
    {
        return Task.FromResult(
            ImageReferenceHelper.GetImageReference(pageNumber, imageNumber, pdfFilePath, extension));
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
        return Task.FromResult(
            ImageReferenceHelper.GetNoOcrPageReferenceAsync(
                request.Filepath!,
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

    public Task<string?> GetNoOcrPageTextLinesAsync(NoOcrServicePageCacheRequest request)
    {
        throw new NotImplementedException();
    }

    public async Task<string?> GetOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath);
        var path = $"/Extractor/Ocr/GetImageText?pageNumber={request.PageNumber}"
            + $"&imageNumber={request.ImageNumber}&filepath={filepath}"
            + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }

    public async Task<string?> GetOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath);
        var path = $"/Extractor/Ocr/GetScreenshotText?pageNumber={request.PageNumber}"
           + $"&imageNumber={request.ImageNumber}&filepath={filepath}"
           + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return content;
    }

    public async Task<List<LineAndWords>> GetTemporaryOcrImageTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath);
        
        var path = $"/Extractor/Ocr/GetTemporaryImageText?pageNumber={request.PageNumber}"
            + $"&imageNumber={request.ImageNumber}&filepath={filepath}"
            + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<LineAndWords>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<List<LineAndWords>> GetTemporaryOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request)
    {
        var filepath = FileHelper.GetFilenameWithoutExtension(request.Filepath);
        var path = $"/Extractor/Ocr/GetTemporaryScreenshotText?pageNumber={request.PageNumber}"
            + $"&filepath={filepath}"
            + $"&ocrServiceName={request.OcrServiceName}&processRunId={request.ProcessRunId}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        return JsonSerializer.Deserialize<List<LineAndWords>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveImageOnPageAsync(
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
        var path = "/Extractor/Images/SaveImageOnPage";

        var json = JsonSerializer.Serialize(new
        {
            Bytes = bytes,
            Width = width,
            Height = height,
            PdfFilePath = pdfFilePath,
            NoOcrServiceName = noOcrServiceName,
            ImageNumber = imageNumber,
            PageNumber = pageNumber,
            Extension = extension,
            ProcessRunId = processRunId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task<NoOcrServiceMetadataCacheRequest> SaveNoOcrPagesMetadataAsync(
        NoOcrServiceMetadataCacheRequest request,
        List<Dictionary<string, object>> pagesMetadata)
    {
        var path = "/Extractor/NoOcr/SaveNoOcrPagesMetadata";
        var pagesMetadataJson = JsonSerializer.Serialize(pagesMetadata, JsonHelper.GetSerializerOptions());
        
        var json = JsonSerializer.Serialize(new
        {
            request.Filepath,
            request.NoOcrServiceName,
            request.ProcessRunId,
            pageLines = pagesMetadataJson
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();

        return request;
    }

    public async Task SaveNoOcrImagesMetadataAsync(NoOcrServiceMetadataCacheRequest request, ImageMetadata imagesMetadata)
    {
        var path = "/Extractor/NoOcr/SaveNoOcrImagesMetadata";

        var json = JsonSerializer.Serialize(new
        {
            request.Filepath,
            request.NoOcrServiceName,
            request.ProcessRunId,
            ImagesMetadata = JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task<NoOcrServicePageCacheRequest> SaveNoOcrPageTextLinesAsync(
        NoOcrServicePageCacheRequest request,
        string pageLines)
    {
        var path = "/Extractor/NoOcr/SaveNoOcrPageTextLines";

        var json = JsonSerializer.Serialize(new
        {
            request.Filepath,
            request.PageNumber,
            request.NoOcrServiceName,
            request.ProcessRunId,
            pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();

        return request;
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrImageText";

        var json = JsonSerializer.Serialize(new
        {
            request.Filepath,
            request.OcrServiceName,
            request.ProcessRunId,
            request.PageNumber,
            request.ImageNumber,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrImageTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrImageText";

        var json = JsonSerializer.Serialize(new
        {
            request.Filepath,
            request.OcrServiceName,
            request.ProcessRunId,
            request.PageNumber,
            request.ImageNumber,
            PageLines = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, string pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrScreenshotText";
        
        var json = JsonSerializer.Serialize(new
        {
            request.Filepath,
            request.PageNumber,
            request.ImageNumber,
            request.OcrServiceName,
            request.ProcessRunId,
            PageLines = pageLines
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveOcrScreenshotTextAsync(OcrServiceImageTextCacheRequest request, List<LineAndWords> pageLines)
    {
        var path = "/Extractor/Ocr/SaveOcrScreenshotText";

        var json = JsonSerializer.Serialize(new
        {
            request.Filepath,
            request.PageNumber,
            request.ImageNumber,
            request.OcrServiceName,
            request.ProcessRunId,
            PageLines = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
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
            request.Filepath,
            request.OcrServiceName,
            request.ProcessRunId,
            Text = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
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
            request.Filepath,
            request.OcrServiceName,
            request.ProcessRunId,
            Text = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions())
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