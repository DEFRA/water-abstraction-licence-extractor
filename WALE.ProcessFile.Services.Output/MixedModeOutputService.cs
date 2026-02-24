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
        return databaseOutputService.GetPageScreenshotDataAsync(pageNumber, pdfServiceName, pdfFilePath);
    }

    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun)
    {
        return apiOutputService.SaveProcessRunAsync(processRun);
    }

    public Task SaveLicenceSetsAsync(
        Dictionary<string, LicenceSet> licenceSets,
        string pdfFilePath,
        int processRunId)
    {
        return databaseOutputService.SaveLicenceSetsAsync(licenceSets, pdfFilePath, processRunId);
    }

    public Task<int> SaveLicenceAsync(
        Licence licence,
        string? pdfFilePath,
        int processRunId)
    {
        return databaseOutputService.SaveLicenceAsync(licence, pdfFilePath, processRunId);
    }

    public Task UpdateLicenceAsync(
        Licence licence,
        int licenceId,
        string? pdfFilePath,
        int processRunId)
    {
        return databaseOutputService.UpdateLicenceAsync(licence, licenceId, pdfFilePath, processRunId);
    }

    public Task SaveMatchAsync(
        int matchesResultId,
        string? labelName,
        string? labelGroupName,
        LabelGroupResult data)
    {
        return databaseOutputService.SaveMatchAsync(matchesResultId, labelName, labelGroupName, data);
    }

    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId)
    {
        return databaseOutputService.SaveMatchResultAsync(matchesResult, pdfFilePath, processRunId);
    }

    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId)
    {
        return databaseOutputService.SaveListDataAsync(listData, processRunId);
    }

    public Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath,
        int processRunId)
    {
        return databaseOutputService.SavePageScreenshotAsync(
            pdfDocument,
            pageNumber,
            noOcrServiceName,
            pdfFilePath,
            processRunId);
    }

    public Task SaveAllPagesTextAsync(
        List<DocumentLine> documentLines,
        string pdfFilePath,
        string noOcrServiceName,
        int processRunId)
    {
        return databaseOutputService.SaveAllPagesTextAsync(
            documentLines,
            pdfFilePath,
            noOcrServiceName,
            processRunId);
    }

    public Task FinishProcessRunAsync(ProcessRun processRun, int regionId)
    {
        return databaseOutputService.FinishProcessRunAsync(processRun, regionId);
    }

    public Task<List<ProcessRun>> GetProcessRunsAsync()
    {
        return databaseOutputService.GetProcessRunsAsync();
    }

    public Task<List<Licence>> GetLicencesAsync(int processRunId)
    {
        return databaseOutputService.GetLicencesAsync(processRunId);
    }

    public Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(
        int processRunId,
        List<Licence> licences)
    {
        return databaseOutputService.GetLicenceSetsAsync(processRunId, licences);
    }

    public Task<List<LicenceSet>> GetLicenceSetsAsync(string filename)
    {
        return databaseOutputService.GetLicenceSetsAsync(filename);
    }

    public Task<Licence?> GetLicenceAsync(string filename)
    {
        return databaseOutputService.GetLicenceAsync(filename);
    }

    public Task<MatchesResult?> GetMatchesResult(string filename)
    {
        return databaseOutputService.GetMatchesResult(filename);
    }
}