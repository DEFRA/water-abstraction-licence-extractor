using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.OutputSchema;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Interfaces;

public interface IOutputService
{
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

    public Task SaveLicenceSetsAsync(IReadOnlyList<LicenceSet> licenceSets, string pdfFilePath, int processRunId);
    
    public Task<int> SaveLicenceAsync(Licence licence, string pdfFilePath, int processRunId);
    
    public Task SaveMatchResultAsync(MatchesResult matchesResult, string pdfFilePath, int processRunId);
    
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