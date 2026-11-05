using WRADI.DocumentType.WrInspectionReport.Models;

namespace WRADI.DocumentType.WrInspectionReport.Interfaces;

public interface IWrInspectionReportSummaryReadService
{
    /// The most recent projection per file, which is what the list view shows - a report is
    /// immutable, so an older run's result for the same file is only ever a staler extraction.
    /// A null date bound is unbounded; rows with no date are returned only when both are null.
    Task<List<WrInspectionReportSummary>> GetLatestPerFileAsync(
        DateOnly? from,
        DateOnly? to,
        string? template,
        int skip,
        int take);

    /// Everything one run projected, which is what a run-scoped list shows.
    Task<List<WrInspectionReportSummary>> GetForProcessRunAsync(int processRunId);

    Task<int> CountForProcessRunAsync(int processRunId);
}
