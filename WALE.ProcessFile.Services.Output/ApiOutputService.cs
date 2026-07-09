using System.Net.Http.Headers;
using System.Text.Json;
using SkiaSharp;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Output;

public class ApiOutputService(HttpClient httpClient) : IOutputService
{
    public string? OutputFolder { get; set; }
    
    public Task SetupAsync()
    {
        return Task.CompletedTask;
    }

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        Guid fileId)
    {
        return ImageReferenceHelper.GetPageScreenshotReferences(pageNumber, pdfServiceName, fileId);
    }

    public async Task<byte[]?> GetPageScreenshotThumbnailAsync(int pageNumber, string pdfServiceName, Guid fileId)
    {
        var path = $"/Extractor/Images/Thumbnail?fileId={fileId}&serviceName={pdfServiceName}&pageNumber={pageNumber}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();
            
        if (string.IsNullOrEmpty(content))
        {
            throw new NullReferenceException("Page screenshot data returned null");
        }

        return JsonSerializer.Deserialize<byte[]?>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<List<byte[]>> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        Guid fileId)
    {
        var path = $"/Extractor/Images/GetPageScreenshot?fileId={fileId}&serviceName={pdfServiceName}&pageNumber={pageNumber}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();
            
        if (string.IsNullOrEmpty(content))
        {
            throw new NullReferenceException("Page screenshot data returned null");
        }

        return JsonSerializer.Deserialize<List<byte[]>?>(content, JsonHelper.GetSerializerOptions())!;
    }

    public async Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun)
    {
        var path = "/Extractor/ProcessRun/Create";

        var json = JsonSerializer.Serialize(new
        {
            processRun.Description,
            processRun.NumberOfFiles,
            processRun.Status
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));

        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        processRun.ProcessRunId = int.Parse(content);
        return processRun;
    }

    public async Task<ProcessRun> MarkProcessRunCompleteIfCompleteAsync(ProcessRun processRun)
    {
        var path = "/Extractor/ProcessRun/MarkProcessRunCompleteIfComplete";

        var json = JsonSerializer.Serialize(new
        {
            processRun.ProcessRunId,
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ProcessRun>(content, JsonHelper.GetSerializerOptions());
        return result!;
    }

    public async Task<ProcessRunFile> AddProcessRunFileAsync(ProcessRunFile processRunFile)
    {
        var path = "/Extractor/ProcessRun/AddProcessRunFile";

        var json = JsonSerializer.Serialize(new
        {
            processRunFile.FileName,
            processRunFile.ProcessRunId,
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        processRunFile.ProcessRunFileId = int.Parse(content);
        return processRunFile;
    }

    public async Task<ProcessRunFile> MarkProcessRunFileCompleteAsync(ProcessRunFile processRunFile)
    {
        var path = "/Extractor/ProcessRun/MarkProcessRunFileComplete";

        var json = JsonSerializer.Serialize(new
        {
            processRunFile.ProcessRunFileId,
            processRunFile.FileName,
            processRunFile.ProcessRunId,
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        processRunFile.ProcessRunFileId = int.Parse(content);
        return processRunFile;
    }

    public async Task<ProcessRunFile> ReportErrorProcessRunFileAsync(ProcessRunFile processRunFile)
    {
        var path = "/Extractor/ProcessRun/ReportErrorProcessRunFile";
      
        var json = JsonSerializer.Serialize(new
        {
            processRunFile.ProcessRunFileId,
            processRunFile.FileName,
            processRunFile.ProcessRunId,
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        processRunFile.ProcessRunFileId = int.Parse(content);
        return processRunFile;
    }

    public async Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, Guid? fileId, int processRunId)
    {
        if (fileId == null)
        {
            return;
        }
        
        var path = "/Extractor/Licence/SaveLicenceSets";

        var json = JsonSerializer.Serialize(new
        {
            fileId,
            processRunId,
            licenceSets = JsonSerializer.Serialize(licenceSets, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveLicenceSetAsync(LicenceSet licenceSet, Guid? fileId, int processRunId)
    {
        if (fileId == null)
        {
            return;
        }
        
        var path = "/Extractor/Licence/SaveLicenceSet";

        var json = JsonSerializer.Serialize(new
        {
            fileId,
            processRunId,
            licenceSet = JsonSerializer.Serialize(licenceSet, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> SaveLicenceAsync(Licence licence, int processRunId)
    {
        var path = "/Extractor/Licence/Save";

        var json = JsonSerializer.Serialize(new
        {
            fileId = licence.DmsFileId,
            processRunId,
            licence = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return int.Parse(content);
    }

    public Task UpdateLicenceAsync(Licence licence, int licenceId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task SaveMatchesAsync(List<(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)> matches)
    {
        var path = "/Extractor/Match/SaveMultiple";

        var json = JsonSerializer.Serialize(new
        {
            matches
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)
    {
        var path = "/Extractor/Match/Save";

        var json = JsonSerializer.Serialize(new
        {
            matchesResultId,
            labelName,
            labelGroupName,
            data = JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> SaveMatchResultAsync(MatchesResult matchesResult, Guid fileId, int processRunId)
    {
        var path = "/Extractor/MatchResult/Save";

        var json = JsonSerializer.Serialize(new
        {
            Matches = matchesResult,
            fileId,
            ProcessRunId = processRunId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return int.Parse(content);
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        throw new NotImplementedException();
    }

    public async Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        int processRunId)
    {
        var images = await pdfDocument.GetPageAsSkBitmapAsync(pageNumber, noOcrServiceName);

        var byteSize = 0;
        var tasks = new List<Task<int>>();
        
        foreach (var (providerName, bitmap) in images)
        {
            tasks.Add(SavePageScreenshotTaskAsync(
                providerName,
                bitmap,
                pageNumber, 
                fileId,
                processRunId));
        }

        foreach (var task in tasks)
        {
            byteSize += await task;
        }
        
        return byteSize;
    }

    private async Task<int> SavePageScreenshotTaskAsync(
        string providerName,
        SKBitmap bitmap,
        int pageNumber,
        Guid fileId,
        int processRunId)
    {
        var bytes = await GetAsJpegAsync(bitmap);

        var dtStart = DateTime.UtcNow;
        var path = $"/Extractor/Images/SavePageScreenshot?pageNumber={pageNumber}&noOcrServiceName={providerName}" +
            $"&fileId={fileId}&processRunId={processRunId}";
        
        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(bytes, 0, bytes.Length);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(imageContent, "image", "image.jpg");
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), form));
        response.EnsureSuccessStatusCode();

        var tsDuration = (DateTime.UtcNow - dtStart).TotalMilliseconds.ToString("0.0");

        if (_showAllLogs)
        {
            ConsoleHelper.WriteLine($"SavePageScreenshot API call (P{pageNumber}, {providerName}) took {tsDuration}ms");
        }

        return bytes.Length;
    }

    public Task SavePageScreenshotInternalAsync(
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        byte[] data,
        int processRunId)
    {
        throw new NotImplementedException();
    }

    private static async Task<byte[]> GetAsJpegAsync(SKBitmap bitmap, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);

        if (image == null)
        {
            throw new FileNotFoundException("Could not load image");
        }
        
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);

        if (data == null)
        {
            throw new FileNotFoundException("Could not encode image");
        }
        
        await using var stream = new MemoryStream();
        data.SaveTo(stream);
        
        await stream.FlushAsync();

        stream.Position = 0;
        var bytes = stream.ToArray();
        stream.Close();

        return bytes;
    }

    public async Task SaveAllPagesTextAsync(
        List<DocumentLine> documentLines,
        Guid fileId,
        string noOcrServiceName,
        int processRunId)
    {
        var path = "/Extractor/NoOcr/SaveAllPagesText";

        var json = JsonSerializer.Serialize(new
        {
            documentLines = JsonSerializer.Serialize(documentLines, JsonHelper.GetSerializerOptions()),
            fileId,
            noOcrServiceName,
            processRunId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task FinishProcessRunAsync(ProcessRun processRun)
    {
        var path = "/Extractor/ProcessRun/Finish";

        var json = JsonSerializer.Serialize(new
        {
            processRun.ProcessRunId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        processRun.EndDateTimeUtc = DateTime.Parse(content).ToUniversalTime();
    }

    public async Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        var path = "/BFF/ProcessRuns/GetProcessRuns";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var processRuns = JsonSerializer.Deserialize<List<ProcessRun>>(content, JsonHelper.GetSerializerOptions());

        if (processRuns == null)
        {
            throw new FileNotFoundException("Could not load processRuns");
        }
        
        return processRuns;
    }
    
    public async Task<List<ProcessRun>> GetAllProcessRunsAsync()
    {
        var path = "/BFF/ProcessRuns/GetAllProcessRuns";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var processRuns = JsonSerializer.Deserialize<List<ProcessRun>>(content, JsonHelper.GetSerializerOptions());
        
        if (processRuns == null)
        {
            throw new FileNotFoundException("Could not load processRuns");
        }
        
        return processRuns;
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId, int skip, int take)
    {
        var path = $"/Extractor/Licence/GetAll?processRunId={processRunId}&skip={skip}&take={take}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(content))
        {
            throw new NullReferenceException("Get all licences returned null");
        }

        return JsonSerializer.Deserialize<List<Licence>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public Task<List<Licence>> GetLicencesSearchAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        throw new NotImplementedException();
    }
    
    public Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(
        int processRunId,
        List<Licence> licences)
    {
        throw new NotImplementedException();
    }

    public async Task<Dictionary<string, LicenceSet>> GetProcessRunLicenceSetsAsync(
        int processRunId)
    {
        var path = $"/BFF/ProcessRuns/GetProcessRunLicenceSets?processRunId={processRunId}";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var licenceSets = JsonSerializer.Deserialize<Dictionary<string, LicenceSet>>(content, JsonHelper.GetSerializerOptions());
        
        if (licenceSets == null)
        {
            throw new FileNotFoundException("Could not load licenceSets");
        }
        
        return licenceSets;
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task<Licence?> GetLicenceAsync(Guid fileId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<MatchesResult?> GetMatchesResult(Guid fileId)
    {
        throw new NotImplementedException();
    }

    public Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber)
    {
        throw new NotImplementedException();
    }

    public async Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId)
    {
        var path = $"/BFF/FileData/LicenceSectionVerifications?licenceFileId={licenceFileId}";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var licenceSectionVerifications = JsonSerializer.Deserialize<IEnumerable<LicenceSectionVerification>>(content, JsonHelper.GetSerializerOptions());
        
        if (licenceSectionVerifications == null)
        {
            throw new FileNotFoundException("Could not load licenceSectionVerifications");
        }
        
        return licenceSectionVerifications;
    }

    public async Task<IEnumerable<LicenceSectionVerification>> GetLatestLicenceSectionVerificationsAsync()
    {
        var path = $"/BFF/FileData/GetLatestLicenceSectionVerifications";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        var licenceSectionVerifications = JsonSerializer.Deserialize<IEnumerable<LicenceSectionVerification>>(content, JsonHelper.GetSerializerOptions());
        
        if (licenceSectionVerifications == null)
        {
            throw new FileNotFoundException("Could not load licenceSectionVerifications");
        }
        
        return licenceSectionVerifications;
    }

    public Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        throw new NotImplementedException();
    }

    public async Task SavePageScreenshotThumbnailAsync(int pageNumber, string serviceName, Guid fileId, byte[] thumbnail, int processRunId)
    {
        var dtStart = DateTime.UtcNow;
        var path = $"/Extractor/Images/SavePageScreenshotThumbnail?pageNumber={pageNumber}&noOcrServiceName={serviceName}" +
                   $"&fileId={fileId}&processRunId={processRunId}";
        
        using var form = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(thumbnail, 0, thumbnail.Length);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        form.Add(imageContent, "image", "image.jpg");
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), form));
        response.EnsureSuccessStatusCode();

        var tsDuration = (DateTime.UtcNow - dtStart).TotalMilliseconds.ToString("0.0");

        if (_showAllLogs)
        {
            ConsoleHelper.WriteLine($"SavePageScreenshotThumbnailAsync API call (P{pageNumber}, {serviceName}) took {tsDuration}ms");
        }
    }

    public async Task<int> GetTotalLicenceCountAsync(int processRunId, ProcessRunQuery processRunQuery)
    {
        var path = $"/BFF/ProcessRuns/GetTotalLicenceCount?processRunId={processRunId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();
            
        if (string.IsNullOrEmpty(content))
        {
            throw new NullReferenceException("Total licence count returned null");
        }

        return JsonSerializer.Deserialize<int>(content, JsonHelper.GetSerializerOptions())!;
    }

    private static bool _showAllLogs = false;
}