using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WRADI.Services.ProcessFile.WrInspectionReport;

/// The orchestrator and the Cmd runner select independently and had drifted into two different
/// answers, so a local run and a deployed run covered different files.
public static class WrInspectionReportCandidateSelector
{
    public readonly record struct CandidateSelection(
        IReadOnlyList<InspectionReportFinderResult> Sendable,
        int WithoutIdentity,
        int NotInIngress);

    public static bool HasIdentity(InspectionReportFinderResult candidate) =>
        !string.IsNullOrEmpty(candidate.PermitNumber)
        && !string.IsNullOrEmpty(candidate.FileId)
        && Guid.TryParse(candidate.FileId, out _);

    /// The DMS file name is deliberately not consulted: it is a .doc for a report since converted,
    /// and a .pdf for one nobody ever uploaded. Checking it queues the latter to fail one per file.
    private static bool IsInIngress(
        InspectionReportFinderResult candidate,
        IReadOnlySet<string> ingressFileNames) =>
        HasIdentity(candidate)
        && ingressFileNames.Contains(
            WrInspectionReportIngressFile.BuildFileName(candidate.PermitNumber!, candidate.FileId!));

    public static CandidateSelection Select(
        IReadOnlyList<InspectionReportFinderResult> candidates,
        IReadOnlySet<string> ingressFileNames)
    {
        var sendable = new List<InspectionReportFinderResult>();
        var withoutIdentity = 0;
        var notInIngress = 0;

        foreach (var candidate in candidates)
        {
            if (!HasIdentity(candidate))
            {
                withoutIdentity++;
            }
            else if (!IsInIngress(candidate, ingressFileNames))
            {
                notInIngress++;
            }
            else
            {
                sendable.Add(candidate);
            }
        }

        return new CandidateSelection(sendable, withoutIdentity, notInIngress);
    }

    /// Compared without case: the bucket is keyed in lowercase, but a key that slipped in with its
    /// original casing would read as absent and silently drop the document from every run.
    public static IReadOnlySet<string> ToIngressFileNameSet(IEnumerable<string> fileNames) =>
        new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);

    /// upload_date is free text from the extract, so a row that will not parse is left out.
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

    /// An unset end is named, not implied: an open window and one ending today diverge as more
    /// documents arrive.
    public static string DescribeUploadWindow(DateOnly? uploadedFrom, DateOnly? uploadedTo) =>
        (uploadedFrom, uploadedTo) switch
        {
            ({ } from, { } to) => $"from {from:yyyy-MM-dd} to {to:yyyy-MM-dd}",
            ({ } from, null) => $"from {from:yyyy-MM-dd} to no end date",
            (null, { } to) => $"from no start date to {to:yyyy-MM-dd}",
            _ => "at any time"
        };
}
