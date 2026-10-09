using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.Tools.Tests;

/// <summary>
/// An <see cref="IOutputService"/> that keeps nothing, for bulk runs that want the extracted
/// matches and none of the by-products.
///
/// <see cref="WALE.ProcessFile.Services.Output.FileSystemOutputService"/> writes a rendered JPEG
/// per page plus the whole document's text, about 11 MB a document. That exists so OCR providers
/// have images to read; a run with no OCR providers configured never looks at them, and across the
/// ~10,300 document WQ corpus it would cost roughly 110 GB to produce files nothing reads.
///
/// Every member is a no-op returning a harmless default. Nothing in the no-OCR extraction path
/// reads back from the output service: screenshot data is only fetched by OCR providers, and the
/// matches-result lookups only happen under lock exclusivity, which these runs turn off.
/// </summary>
public class NoPersistenceOutputService : IOutputService
{
    public string? OutputFolder { get; set; }

    public Task SetupAsync() => Task.CompletedTask;

    public List<(string ProviderName, string? ImageReference)> GetPageScreenshotReferences(
        int pageNumber,
        string pdfServiceName,
        Guid fileId) => [];

    public Task<byte[]?> GetPageScreenshotThumbnailAsync(
        int pageNumber,
        string pdfServiceName,
        Guid fileId) => Task.FromResult<byte[]?>(null);

    public Task<List<byte[]>> GetPageScreenshotDataAsync(
        int pageNumber,
        string pdfServiceName,
        Guid fileId) => Task.FromResult(new List<byte[]>());

    public Task<ProcessRun> StartProcessRunAsync(ProcessRun processRun) => Task.FromResult(processRun);

    public Task<ProcessRun> MarkProcessRunCompleteIfCompleteAsync(ProcessRun processRun) =>
        Task.FromResult(processRun);

    public Task<ProcessRunFile> AddProcessRunFileAsync(ProcessRunFile processRunFile) =>
        Task.FromResult(processRunFile);

    public Task<ProcessRunFile> MarkProcessRunFileCompleteAsync(ProcessRunFile processRunFile) =>
        Task.FromResult(processRunFile);

    public Task<ProcessRunFile> ReportErrorProcessRunFileAsync(ProcessRunFile processRunFile) =>
        Task.FromResult(processRunFile);

    public Task SaveMatchesAsync(
        List<(int matchesResultId, string? labelName, string? labelGroupName, LabelGroupResult data)> matches) =>
        Task.CompletedTask;

    public Task SaveMatchAsync(
        int matchesResultId,
        string? labelName,
        string? labelGroupName,
        LabelGroupResult data) => Task.CompletedTask;

    public Task<int> SaveStubMatchesResultAsync(string filename, Guid fileId, int processRunId) =>
        Task.FromResult(-1);

    public Task<int> SaveErrorMatchesResultAsync(
        string filename,
        Guid fileId,
        int processRunId,
        string? error,
        bool isUpdate) => Task.FromResult(-1);

    public Task<int> SaveMatchResultAsync(
        MatchesResult matchesResult,
        Guid fileId,
        int processRunId,
        bool isUpdate) => Task.FromResult(-1);

    public Task<int> SavePageScreenshotAsync(
        PdfDocument pdfDocument,
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        int processRunId) => Task.FromResult(-1);

    public Task SavePageScreenshotInternalAsync(
        int pageNumber,
        string noOcrServiceName,
        Guid fileId,
        byte[] data,
        int processRunId) => Task.CompletedTask;

    public Task SaveAllPagesTextAsync(
        List<DocumentLine> documentLines,
        Guid fileId,
        string noOcrServiceName,
        int processRunId) => Task.CompletedTask;

    public Task<List<ProcessRun>> GetProcessRunsAsync() => Task.FromResult(new List<ProcessRun>());

    public Task<List<ProcessRun>> GetAllProcessRunsAsync() => Task.FromResult(new List<ProcessRun>());

    public Task<MatchesResult?> GetMatchesResultAsync(Guid fileId) =>
        Task.FromResult<MatchesResult?>(null);

    public Task<MatchesResult?> GetMatchesResultAsync(Guid fileId, int processRunId) =>
        Task.FromResult<MatchesResult?>(null);

    public Task SavePageScreenshotThumbnailAsync(
        int pageNumber,
        string serviceName,
        Guid fileId,
        byte[] thumbnail,
        int processRunId) => Task.CompletedTask;

    public Task UpdateProcessRunByLicenceNumbersAsync(int processRunId, string[] licenceNumbers) =>
        Task.CompletedTask;

    public Task UpdateLicenceListProcessRunAsync(int processRunId) => Task.CompletedTask;

    public Task<List<MatchResultSimple>> GetSimpleMatchResults(int processRunId) =>
        Task.FromResult(new List<MatchResultSimple>());
}
