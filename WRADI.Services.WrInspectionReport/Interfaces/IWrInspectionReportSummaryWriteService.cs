using WRADI.DocumentType.WrInspectionReport.Models;

namespace WRADI.DocumentType.WrInspectionReport.Interfaces;

public interface IWrInspectionReportSummaryWriteService
{
    /// Replaces the projection for one process run. Derived data, so rebuilding is always safe.
    Task<int> ReplaceForProcessRunAsync(int processRunId, IReadOnlyList<WrInspectionReportSummary> summaries);
}
