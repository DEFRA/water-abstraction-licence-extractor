namespace WRADI.DocumentType.WrInspectionReport.Interfaces;

public interface IWrInspectionReportProjectionService
{
    /// Projects every result a process run holds into wr_inspection_report_summary, replacing
    /// whatever was there for that run. Derived data, so it is safe to call repeatedly, and safe
    /// against a run still in progress - it projects whatever has landed so far.
    Task<WrInspectionReportProjectionOutcome> ProjectProcessRunAsync(
        int processRunId,
        CancellationToken cancellationToken = default);
}

/// What a projection run did, so a caller can log it without re-querying.
public record WrInspectionReportProjectionOutcome(int Written, int SkippedUndeserializable);
