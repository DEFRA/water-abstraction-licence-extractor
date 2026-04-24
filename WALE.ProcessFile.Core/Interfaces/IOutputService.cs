using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IOutputService
{
    public string? OutputFolder { get; set; }
    
    public Task SetupAsync();
    
    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        Guid fileId);
    
    public Task<List<byte[]>> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        Guid fileId);
    
    public Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun);

    public Task SaveLicenceSetsAsync(Dictionary<string, LicenceSet> licenceSets, Guid? fileId, int processRunId);
    
    public Task<int> SaveLicenceAsync(Licence licence, int processRunId);

    public Task UpdateLicenceAsync(Licence licence, int licenceId, int processRunId);
    
    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data);
    
    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, Guid fileId, int processRunId);
    
    public Task SaveListDataAsync(List<OutputListDataItem> listData, int processRunId);
    
    public Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        int processRunId);

    public Task SavePageScreenshotInternalAsync(
        int pageNumber,
        string noOcrServiceName,
        Guid fileId, 
        byte[] data,
        int processRunId);
    
    public Task SaveAllPagesTextAsync(
        List<DocumentLine> documentLines,
        Guid fileId,
        string noOcrServiceName,
        int processRunId);
    
    Task FinishProcessRunAsync(ProcessRun processRun, int regionId);
    
    Task<List<ProcessRun>> GetProcessRunsAsync();
    
    Task<List<Licence>> GetLicencesAsync(int processRunId);

    Task<Dictionary<string, LicenceSet>> GetLicenceSetsAsync(int processRunId, List<Licence> licences);
    
    Task<List<LicenceSet>> GetLicenceSetsAsync(Guid fileId);
    
    Task<Licence?> GetLicenceAsync(Guid fileId, int? processRunId = null);
    
    Task<MatchesResult?> GetMatchesResult(Guid fileId);
    
    Task<LinkedLicence[]?> GetLinkedLicencesAsync(string permitNumber);

    Task<IEnumerable<LicenceSectionVerification>> GetLicenceSectionVerificationsAsync(Guid licenceFileId);

    Task<IEnumerable<LicenceSectionVerification>> GetLatestLicenceSectionVerificationsAsync();

    Task<int> SaveLicenceSectionVerificationAsync(LicenceSectionVerification verification);
}