using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Output;

public class ApiOutputService(HttpClient httpClient) : IOutputService
{
    public string? OutputFolder { get; set; }
    
    public Task SetupAsync()
    {
        throw new NotImplementedException();
    }

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public Task<List<byte[]>> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        throw new NotImplementedException();
    }

    public async Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
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

    public Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, string pdfFilePath, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<int> SaveLicenceAsync(Licence licence, string? pdfFilePath, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task UpdateLicenceAsync(Licence licence, int licenceId, string? pdfFilePath, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)
    {
        throw new NotImplementedException();
    }

    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath,
        int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task SaveAllPagesTextAsync(
        List<DocumentLine> documentLines,
        string pdfFilePath,
        string noOcrServiceName,
        int processRunId)
    {
        throw new NotImplementedException();
    }

    public Task FinishProcessRunAsync(ProcessRun processRun, int regionId)
    {
        throw new NotImplementedException();
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        throw new NotImplementedException();
    }

    public Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        throw new NotImplementedException();
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
}