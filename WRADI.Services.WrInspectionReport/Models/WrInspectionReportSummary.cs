using WRADI.DocumentType.WrInspectionReport.Enums;

namespace WRADI.DocumentType.WrInspectionReport.Models;

/// A projection of one extracted report, holding only what a list view sorts and filters on.
/// Derived entirely from matches_result and the discovery result, so it can be dropped and rebuilt.
public class WrInspectionReportSummary
{
    public int ProcessRunId { get; set; }

    public Guid FileId { get; set; }

    public int MatchesResultId { get; set; }

    /// When the inspection happened, per the document.
    public DateOnly? InspectionDate { get; set; }

    /// The form's own metadata date.
    public DateOnly? FormDate { get; set; }

    /// When DMS recorded the document. Absent for some files.
    public DateOnly? DmsDocumentDate { get; set; }

    /// When the file was uploaded to DMS.
    public DateOnly? DmsUploadDate { get; set; }

    /// The date a list orders on, and which of the four it came from. Without the source, a filing
    /// date standing in for an inspection date is indistinguishable from the real thing.
    public DateOnly? SortDate { get; set; }

    public WrInspectionReportDateSource DateSource { get; set; }

    public WrInspectionReportDateWarning DateWarning { get; set; }

    public string? PermitNumber { get; set; }

    public string? Template { get; set; }

    public int Completeness { get; set; }

    public bool IsScan { get; set; }
}
