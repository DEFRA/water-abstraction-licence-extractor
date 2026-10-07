using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Interfaces;

public interface IOutputService
{
    public string? OutputFolder { get; set; }

    public Task SetupAsync();

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        Guid fileId);

    public Task<byte[]?> GetPageScreenshotThumbnailAsync(
        int pageNumber,
        string pdfServiceName,
        Guid fileId);

    public Task<List<byte[]>> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        Guid fileId);

    public Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun);

    public Task<ProcessRun> MarkProcessRunCompleteIfCompleteAsync(ProcessRun processRun);

    public Task<ProcessRunFile> AddProcessRunFileAsync(ProcessRunFile processRunFile);

    public Task<ProcessRunFile> MarkProcessRunFileCompleteAsync(ProcessRunFile processRunFile);

    public Task<ProcessRunFile> ReportErrorProcessRunFileAsync(ProcessRunFile processRunFile);
    
    public Task SaveMatchesAsync(
        List<(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)> matches);

    public Task SaveMatchAsync(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data);

    /// <param name="isUpdate">True to reclaim an existing row for this file/run (e.g. one left in
    /// <see cref="ScrapeStatus.TransientError"/> by an earlier attempt) instead of inserting a second one.</param>
    public Task<int> SaveStubMatchesResultAsync(string filename, Guid fileId, int processRunId, bool isUpdate = false);
    
    /// <param name="status"><see cref="ScrapeStatus.Error"/> (terminal for the run) or
    /// <see cref="ScrapeStatus.TransientError"/> (a retry may reclaim the row).</param>
    public Task<int> SaveErrorMatchesResultAsync(
        string filename,
        Guid fileId,
        int processRunId,
        string? error,
        bool isUpdate,
        ScrapeStatus status = ScrapeStatus.Error);
    
    public Task<int> SaveMatchResultAsync(MatchesResult matchesResult, Guid fileId, int processRunId, bool isUpdate);

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

    Task<List<ProcessRun>> GetProcessRunsAsync();

    Task<List<ProcessRun>> GetAllProcessRunsAsync();

    Task<MatchesResult?> GetMatchesResultAsync(int matchesResultId);

    Task<MatchesResult?> GetMatchesResultAsync(Guid fileId);

    Task<MatchesResult?> GetMatchesResultAsync(Guid fileId, int processRunId);

    Task SaveThumbnailAsync(Guid fileId);
    
    Task SavePageScreenshotThumbnailAsync(
        int pageNumber,
        string serviceName,
        Guid fileId,
        byte[] thumbnail,
        int processRunId);
    
    Task UpdateProcessRunByLicenceNumbersAsync(
        int processRunId,
        string[] licenceNumbers);

    Task UpdateLicenceListProcessRunAsync(
        int processRunId);
    
    Task<List<MatchResultSimple>> GetSimpleMatchResults(int processRunId);
}