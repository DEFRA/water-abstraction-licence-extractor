using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Constants;

namespace WRADI.DocumentType.WrInspectionReport.Services;

/// <summary>
/// WRADI-418. Projects the v3 form's fields onto the shared field names, so a v3 document
/// populates the same model and the same CSV columns as a v1 one.
///
/// Additive and idempotent: it appends shared-named results and leaves the V3-named ones in
/// place, so a second call changes nothing.
/// </summary>
public static class WrInspectionV3SharedFieldMapper
{
    /// <summary>
    /// Not exhaustive: criticality, contact email, the end time and the four block captures have
    /// no v1 counterpart, and the converter reads those from their V3 names directly.
    /// </summary>
    private static readonly (string V3FieldName, string SharedFieldName)[] Aliases =
    [
        (WrInspectionReportFieldNames.V3LicenceNumber, WrInspectionReportFieldNames.LicenceNumber),
        (WrInspectionReportFieldNames.V3AssessmentType, WrInspectionReportFieldNames.InspectionClass),
        (WrInspectionReportFieldNames.V3SiteContactNumber, WrInspectionReportFieldNames.TelephoneNumber),
        (WrInspectionReportFieldNames.V3SiteAddress, WrInspectionReportFieldNames.SiteAddress),
        (WrInspectionReportFieldNames.V3PersonMetWith, WrInspectionReportFieldNames.MetWith),
        (WrInspectionReportFieldNames.V3JobTitleOrRole, WrInspectionReportFieldNames.Position),
        (WrInspectionReportFieldNames.V3AssessmentDate, WrInspectionReportFieldNames.InspectionDate),
        (WrInspectionReportFieldNames.V3InspectionStartTime, WrInspectionReportFieldNames.Time),
        (WrInspectionReportFieldNames.V3InspectingOfficers, WrInspectionReportFieldNames.InspectingOfficer),
        (WrInspectionReportFieldNames.V3GeneralComments, WrInspectionReportFieldNames.GeneralComments),
        (WrInspectionReportFieldNames.V3DocumentTemplateVersion, WrInspectionReportFieldNames.DocumentTemplateVersion)
    ];

    private const string PageFooterPrefix = "Document Template Version";

    /// <summary>
    /// The v3 page footer, which lands mid-value wherever a page break falls inside a multi-line
    /// capture - it turned up inside the conditions list, the enforcement response and the
    /// general comments. Page furniture, not content.
    /// </summary>
    public static string? StripPageFurniture(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return text;
        }

        var lines = text
            .ReplaceLineEndings("\n")
            .Split('\n')
            .Where(line => !IsPageFooter(line))
            .ToList();

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[0]))
        {
            lines.RemoveAt(0);
        }

        while (lines.Count > 0 && string.IsNullOrWhiteSpace(lines[^1]))
        {
            lines.RemoveAt(lines.Count - 1);
        }

        var stripped = string.Join("\n", lines);
        return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
    }

    private static bool IsPageFooter(string line) =>
        line.TrimStart().StartsWith(PageFooterPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The general comments heading wraps, so its own last word ("etc.") is the first line the
    /// rule captures. SkipNextLineWhenStartsWith was tried on the rule and had no effect on this
    /// shape.
    /// </summary>
    private static IReadOnlyList<DocumentLine> TidyLines(string fieldName, IReadOnlyList<DocumentLine> lines)
    {
        var tidied = lines.Where(line => !IsPageFooter(line.Text)).ToList();

        if (fieldName == WrInspectionReportFieldNames.GeneralComments)
        {
            while (tidied.Count > 0
                && tidied[0].Text.Trim().TrimEnd('.').Equals("etc", StringComparison.OrdinalIgnoreCase))
            {
                tidied.RemoveAt(0);
            }
        }

        return tidied;
    }

    public static void Apply(MatchesResult matchesResult)
    {
        var matches = matchesResult.Matches;

        if (matches == null || matches.Count == 0)
        {
            return;
        }

        var added = new List<LabelGroupResult>();

        foreach (var (v3FieldName, sharedFieldName) in Aliases)
        {
            if (Find(matches, sharedFieldName) != null)
            {
                continue;
            }

            var source = Find(matches, v3FieldName);

            if (source?.Text == null || source.Text.Count == 0)
            {
                continue;
            }

            added.Add(Rename(source, sharedFieldName, TidyLines(sharedFieldName, source.Text)));
        }

        AddNameAndAddress(matches, added);

        if (added.Count == 0)
        {
            return;
        }

        matchesResult.Matches = matches.Concat(added).ToList();
    }

    /// <summary>
    /// v3 prints the holder's name and address as two separate cells where v1 captures them as
    /// one block. Joined name-first, the reading order of the v1 form.
    /// </summary>
    private static void AddNameAndAddress(List<LabelGroupResult> matches, List<LabelGroupResult> added)
    {
        if (Find(matches, WrInspectionReportFieldNames.NameAndAddress) != null)
        {
            return;
        }

        var name = Find(matches, WrInspectionReportFieldNames.V3LicenceHolderName);
        var address = Find(matches, WrInspectionReportFieldNames.V3LicenceHolderAddress);

        var lines = (name?.Text ?? [])
            .Concat(address?.Text ?? [])
            .ToList();

        if (lines.Count == 0)
        {
            return;
        }

        added.Add(Rename(
            name ?? address!,
            WrInspectionReportFieldNames.NameAndAddress,
            TidyLines(WrInspectionReportFieldNames.NameAndAddress, lines)));
    }

    private static LabelGroupResult? Find(List<LabelGroupResult> matches, string fieldName) =>
        matches.FirstOrDefault(match => match.MatchedLabelName == fieldName);

    /// <summary>
    /// LabelToMatch.Name is init-only and MatchedLabelName's setter is a no-op while MatchedLabel
    /// is set, so a Clone() cannot be renamed - it reads back under the old name. Building fresh
    /// loses the computed Confidence, which nothing downstream of the form reads.
    /// </summary>
    private static LabelGroupResult Rename(
        LabelGroupResult source,
        string fieldName,
        IReadOnlyList<DocumentLine> text) =>
        new()
        {
            LabelGroupName = fieldName,
            MatchedLabelName = fieldName,
            ServiceName = source.ServiceName,
            MatchedPosition = source.MatchedPosition,
            Text = text,
            LabelStartPageNumber = source.LabelStartPageNumber,
            LabelStartLineNumber = source.LabelStartLineNumber,
            LabelStartCharPosition = source.LabelStartCharPosition,
            LabelEndLineNumber = source.LabelEndLineNumber,
            LabelEndCharPosition = source.LabelEndCharPosition
        };
}
