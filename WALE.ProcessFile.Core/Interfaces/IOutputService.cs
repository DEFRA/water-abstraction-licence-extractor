using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IOutputService
{
    public string? OutputFolder { get; set; }
    
    public Task SetupAsync();
    
    public Task<string> GetPageScreenshotReferenceAsync(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath);
    
    public Task<byte[]?> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        string pdfFilePath);
    
    public Task<ProcessRun> SaveProcessRunAsync(ProcessRun processRun);

    public Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, string pdfFilePath, int processRunId);
    
    public Task<int> SaveLicenceAsync(Licence licence, string? pdfFilePath, int processRunId);

    public Task UpdateLicenceAsync(Licence licence, int licenceId, string? pdfFilePath, int processRunId);
    
    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data);
    
    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId);
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId);
    
    public Task SavePageScreenshotIfDoesntExistAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        string pdfFilePath,
        int processRunId);

    public Task SaveAllPagesTextIfDoesntExistAsync(List<DocumentLine> documentLines, string pdfFilePath, string noOcrServiceName, int processRunId);
    
    Task FinishProcessRunAsync(ProcessRun processRun);
    
    Task<List<ProcessRun>> GetProcessRunsAsync();
    
    Task<List<Licence>> GetLicencesAsync(int processRunId);

    Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(int processRunId, List<Licence> licences);
    
    Task<List<LicenceSet>> GetLicenceSetsAsync(string filename);
    
    Task<Licence?> GetLicenceAsync(string filename);
    
    Task<MatchesResult?> GetMatchesResult(string filename);
}