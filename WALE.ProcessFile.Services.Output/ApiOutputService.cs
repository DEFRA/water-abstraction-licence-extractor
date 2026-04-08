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
        string pdfFilename)
    {
        return ImageReferenceHelper.GetPageScreenshotReferences(pageNumber, pdfServiceName, pdfFilename);
    }

    public async Task<List<byte[]>> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        string pdfFilename)
    {
        var path = $"/Extractor/Images/GetPageScreenshot?filename={pdfFilename}&serviceName={pdfServiceName}&pageNumber={pageNumber}";

        var response = await httpClient.GetAsync(path);
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
            processRun.NumberOfFiles
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();

        processRun.ProcessRunId = int.Parse(content);
        return processRun;
    }

    public async Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, string pdfFilename, int processRunId)
    {
        var path = "/Extractor/Licence/SaveLicenceSets";

        var json = JsonSerializer.Serialize(new
        {
            pdfFilename,
            processRunId,
            licenceSets = JsonSerializer.Serialize(licenceSets, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> SaveLicenceAsync(Licence licence, string? pdfFilename, int processRunId)
    {
        var path = "/Extractor/Licence/Save";

        var json = JsonSerializer.Serialize(new
        {
            pdfFilename,
            processRunId,
            licence = JsonSerializer.Serialize(licence, JsonHelper.GetSerializerOptions())
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
        
        var content = await response.Content.ReadAsStringAsync();
        return int.Parse(content);
    }

    public Task UpdateLicenceAsync(Licence licence, int licenceId, string? pdfFilename, int processRunId)
    {
        throw new NotImplementedException();
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
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task<int> SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilename, int processRunId)
    {
        var path = "/Extractor/MatchResult/Save";

        var json = JsonSerializer.Serialize(new
        {
            Matches = matchesResult,
            pdfFilename,
            ProcessRunId = processRunId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
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
        string pdfFilename,
        int processRunId)
    {
        var filenameNoExtension = pdfDocument.PdfFilenameNoExtension;
        var images = await pdfDocument.GetPageAsSkBitmapAsync(pageNumber, noOcrServiceName);

        var byteSize = 0;
        var tasks = new List<Task<int>>();
        
        foreach (var (providerName, bitmap) in images)
        {
            tasks.Add(SavePageScreenshotTaskAsync(
                providerName,
                bitmap,
                pageNumber, 
                filenameNoExtension,
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
        string pdfFilename,
        int processRunId)
    {
        var bytes = await GetAsJpegAsync(bitmap);
            
        const string path = "/Extractor/Images/SavePageScreenshot";

        var json = JsonSerializer.Serialize(new
        {
            PageNumber = pageNumber,
            NoOcrServiceName = providerName,
            PdfFilename = pdfFilename,
            Data = bytes,
            ProcessRunId = processRunId
        }, JsonHelper.GetSerializerOptions());
            
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();

        return bytes.Length;
    }

    public Task SavePageScreenshotInternalAsync(int pageNumber, string noOcrServiceName, string pdfFilename, byte[] data,
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
        string pdfFilename,
        string noOcrServiceName,
        int processRunId)
    {
        var path = "/Extractor/NoOcr/SaveAllPagesText";

        var json = JsonSerializer.Serialize(new
        {
            documentLines = JsonSerializer.Serialize(documentLines, JsonHelper.GetSerializerOptions()),
            pdfFilename,
            noOcrServiceName,
            processRunId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task FinishProcessRunAsync(ProcessRun processRun, int regionId)
    {
        var path = "/Extractor/ProcessRun/Finish";

        var json = JsonSerializer.Serialize(new
        {
            processRun.ProcessRunId,
            regionCode = regionId
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        processRun.EndDateTimeUtc = DateTime.Parse(content);
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        throw new NotImplementedException();
    }

    public async Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        var path = $"/Extractor/Licence/GetAll?processRunId={processRunId}";

        var response = await httpClient.GetAsync(path);
        var content = await response.Content.ReadAsStringAsync();

        if (string.IsNullOrEmpty(content))
        {
            throw new NullReferenceException("Get all licences returned null");
        }

        return JsonSerializer.Deserialize<List<Licence>>(content, JsonHelper.GetSerializerOptions())!;
    }

    public Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(
        int processRunId,
        List<Licence> licences)
    {
        throw new NotImplementedException();
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<Licence?> GetLicenceAsync(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<MatchesResult?> GetMatchesResult(string filename)
    {
        throw new NotImplementedException();
    }

    public Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification)
    {
        throw new NotImplementedException();
    }
}