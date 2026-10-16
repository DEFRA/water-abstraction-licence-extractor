using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WRADI.Services.ProcessFile.WrInspectionReport;

/// Which discovered documents a run takes. Shared because the orchestrator and the local Cmd
/// runner select candidates independently - the Cmd runner bypasses SQS - and had drifted into
/// two different answers, so a local run and a deployed run covered different files.
public static class WrInspectionReportCandidateSelector
{
    /// The formats discovery finds that the extraction pipeline has no reader for. Rejecting only
    /// what is positively identifiable: a candidate whose file name is absent still passes, since
    /// the ingress key is built from the permit number and file id rather than the name, and
    /// treating unknown as unreadable would silently shrink runs.
    private static readonly string[] UnreadableExtensions = [".doc", ".docx"];

    /// A candidate the pipeline can act on at all: enough identity to build an ingress key, and a
    /// file something can read. WrInspectionReportIngressFile.BuildFileName names every key
    /// ".pdf" regardless of the source, so without the format check a Word document would be
    /// fetched as a PDF that does not exist and fail per file.
    public static bool IsSendable(InspectionReportFinderResult candidate) =>
        !string.IsNullOrEmpty(candidate.PermitNumber)
        && !string.IsNullOrEmpty(candidate.FileId)
        && Guid.TryParse(candidate.FileId, out _)
        && IsProcessable(candidate.FileName);

    public static bool IsProcessable(string? fileName) =>
        string.IsNullOrWhiteSpace(fileName)
        || !UnreadableExtensions.Any(extension =>
            fileName.EndsWith(extension, StringComparison.OrdinalIgnoreCase));

    /// Selects on what DMS recorded, so a row whose upload_date will not parse has no date to
    /// compare and is left out. The column is free text from the extract.
    public static bool IsWithinUploadWindow(
        InspectionReportFinderResult candidate,
        DateOnly? uploadedFrom,
        DateOnly? uploadedTo)
    {
        var uploaded = WrInspectionReportSummaryBuilder.ParseDmsDate(candidate.UploadDate);

        if (uploaded == null)
        {
            return false;
        }

        return (!uploadedFrom.HasValue || uploaded >= uploadedFrom)
            && (!uploadedTo.HasValue || uploaded <= uploadedTo);
    }

    public static bool HasUploadWindow(DateOnly? uploadedFrom, DateOnly? uploadedTo) =>
        uploadedFrom.HasValue || uploadedTo.HasValue;

    public static int CountWithoutUsableUploadDate(IEnumerable<InspectionReportFinderResult> candidates) =>
        candidates.Count(candidate =>
            WrInspectionReportSummaryBuilder.ParseDmsDate(candidate.UploadDate) == null);

    /// Phrased as the bounds that were set, so a run's description reads back as the window that
    /// produced it. An unset end is named rather than implied, because an open window and one
    /// that happens to end today select different files as more documents arrive.
    public static string DescribeUploadWindow(DateOnly? uploadedFrom, DateOnly? uploadedTo) =>
        (uploadedFrom, uploadedTo) switch
        {
            ({ } from, { } to) => $"from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            ({ } from, null) => $"from {from:yyyy-MM-dd} to no end date",
            (null, { } to) => $"from no start date to {to:yyyy-MM-dd}",
            _ => "at any time"
        };
}
