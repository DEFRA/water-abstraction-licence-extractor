using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Output;

public class MixedModeOutputService(
    ApiOutputService apiOutputService,
    DatabaseOutputService databaseOutputService)
    : IOutputService
{
    public string? OutputFolder { get; set; }
    
    public Task SetupAsync()
    {
        return databaseOutputService.SetupAsync();
    }

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        return databaseOutputService.GetPageScreenshotReferences(pageNumber, pdfServiceName, pdfFilePath);
    }

    public Task<List<byte[]>> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath)
    {
        return apiOutputService.GetPageScreenshotDataAsync(pageNumber, pdfServiceName, pdfFilePath);
    }

    public Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun)
    {
        return apiOutputService.StartProcessRunAsync(processRun);
    }

    public Task SaveLicenceSetsAsync(
        Dictionary<string, LicenceSet> licenceSets,
        string pdfFilePath,
        int processRunId)
    {
        return apiOutputService.SaveLicenceSetsAsync(licenceSets, pdfFilePath, processRunId);
    }

    public Task<int> SaveLicenceAsync(
        Licence licence,
        string? pdfFilePath,
        int processRunId)
    {
        return apiOutputService.SaveLicenceAsync(licence, pdfFilePath, processRunId);
    }

    public Task UpdateLicenceAsync(
        Licence licence,
        int licenceId,
        string? pdfFilePath,
        int processRunId)
    {
        return apiOutputService.UpdateLicenceAsync(licence, licenceId, pdfFilePath, processRunId);
    }

    public Task SaveMatchAsync(
        int matchesResultId,
        string? labelName,
        string? labelGroupName,
        LabelGroupResult data)
    {
        return apiOutputService.SaveMatchAsync(matchesResultId, labelName, labelGroupName, data);
    }

    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId)
    {
        return apiOutputService.SaveMatchResultAsync(matchesResult, pdfFilePath, processRunId);
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        return apiOutputService.SaveListDataAsync(listData, processRunId);
    }

    public Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath,
        int processRunId)
    {
        return apiOutputService.SavePageScreenshotAsync(
            pdfDocument,
            pageNumber,
            noOcrServiceName,
            pdfFilePath,
            processRunId);
    }

    public Task SavePageScreenshotInternalAsync(int pageNumber, string noOcrServiceName, string pdfFilename, byte[] data,
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
        return apiOutputService.SaveAllPagesTextAsync(
            documentLines,
            pdfFilePath,
            noOcrServiceName,
            processRunId);
    }

    public Task FinishProcessRunAsync(ProcessRun processRun, int regionId)
    {
        return apiOutputService.FinishProcessRunAsync(processRun, regionId);
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        return apiOutputService.GetProcessRunsAsync();
    }

    public Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        return apiOutputService.GetLicencesAsync(processRunId);
    }

    public Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(
        int processRunId,
        List<Licence> licences)
    {
        return apiOutputService.GetLicenceSetsAsync(processRunId, licences);
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(string filename)
    {
        return apiOutputService.GetLicenceSetsAsync(filename);
    }

    public Task<Licence?> GetLicenceAsync(string filename)
    {
        return apiOutputService.GetLicenceAsync(filename);
    }

    public Task<MatchesResult?> GetMatchesResult(string filename)
    {
        return apiOutputService.GetMatchesResult(filename);
    }
}