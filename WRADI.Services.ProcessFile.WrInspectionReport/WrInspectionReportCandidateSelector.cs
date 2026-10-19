using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WRADI.Services.ProcessFile.WrInspectionReport;

/// Which discovered documents a run takes. Shared because the orchestrator and the local Cmd
/// runner select candidates independently - the Cmd runner bypasses SQS - and had drifted into
/// two different answers, so a local run and a deployed run covered different files.
public static class WrInspectionReportCandidateSelector
{
    /// <summary>
    /// What became of a set of candidates. The two rejection reasons are counted separately
    /// because they mean different things to whoever reads the log: one is a discovery record
    /// nothing can be done with, the other is a document waiting to be collected or converted.
    /// </summary>
    public readonly record struct CandidateSelection(
        IReadOnlyList<InspectionReportFinderResult> Sendable,
        int WithoutIdentity,
        int NotInIngress);

    /// <summary>
    /// Enough identity to build an ingress key: the key is built from the permit number and file
    /// id, and nothing else about the discovery record is required.
    /// </summary>
    public static bool HasIdentity(InspectionReportFinderResult candidate) =>
        !string.IsNullOrEmpty(candidate.PermitNumber)
        && !string.IsNullOrEmpty(candidate.FileId)
        && Guid.TryParse(candidate.FileId, out _);

    /// <summary>
    /// Whether the object this candidate resolves to is in the ingress bucket. The DMS file name
    /// is deliberately not consulted - it is a .doc for a report that has since been converted, and
    /// a .pdf for one nobody ever uploaded. This is also what excludes the latter, which are
    /// otherwise queued and fail one per file.
    /// </summary>
    private static bool IsInIngress(
        InspectionReportFinderResult candidate,
        IReadOnlySet<string> ingressFileNames) =>
        HasIdentity(candidate)
        && ingressFileNames.Contains(
            WrInspectionReportIngressFile.BuildFileName(candidate.PermitNumber!, candidate.FileId!));

    /// <summary>
    /// Partitions candidates into those a run can act on - enough identity to build an ingress key,
    /// and an object at it - and the two reasons the rest were left out.
    /// </summary>
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

    /// <summary>
    /// The ingress keys as the selector needs them: a set, compared without case. The bucket is
    /// keyed in lowercase and BuildFileName lowercases, but a key that slipped in with its original
    /// casing would otherwise read as absent and silently drop the document from every run.
    /// </summary>
    public static IReadOnlySet<string> ToIngressFileNameSet(IEnumerable<string> fileNames) =>
        new HashSet<string>(fileNames, StringComparer.OrdinalIgnoreCase);

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
