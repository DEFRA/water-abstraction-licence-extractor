using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;
using WRADI.DocumentType.WrInspectionReport.Constants;

namespace WRADI.DocumentType.WrInspectionReport.Services;

/// <summary>
/// WRADI-418. Resolves the v3 form's fields by reading its two-column key/value tables, and
/// overlays the results onto whatever the label rules produced.
///
/// Anything resolved here replaces the label-rule result for that field; anything it cannot
/// resolve keeps whatever the rules found, so this can only add, never take a field away.
///
/// A rule opts its field in with FromLetterAndKeyValueTable(), and the rule's own start text is
/// the lookup key.
/// </summary>
public static class WrInspectionV3KeyValueMatcher
{
    /// <summary>The key that starts each repeat of the meter group.</summary>
    public const string MeterGroupFirstKey = "Meter location or reference";

    /// <summary>
    /// Takes tables already fetched: one GetTablesAsync call per document however much is read
    /// from it, which matters as soon as the extractor is a paid one.
    /// </summary>
    public static void Apply(
        MatchesResult item,
        IReadOnlyList<DocumentTable> tables,
        List<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        string serviceName)
    {
        if (tables.Count == 0)
        {
            return;
        }

        var pairs = KeyValueTableHelper.ReadPairs(tables);

        if (pairs.Count == 0)
        {
            return;
        }

        var resolved = new Dictionary<string, LabelGroupResult>();

        foreach (var labelGroup in labelLookups)
        {
            var label = labelGroup.Labels.FirstOrDefault(l =>
                l.LayoutExtractorTableLookupType == LayoutExtractorTableLookupType.KeyValue
                && l.TextStart != null);

            if (label?.TextStart == null)
            {
                continue;
            }

            var value = label.TextStart
                .Select(textStart => KeyValueTableHelper.FindValue(pairs, textStart.Text))
                .FirstOrDefault(found => !string.IsNullOrWhiteSpace(found));

            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            resolved[labelGroup.LabelGroupName] = BuildResult(
                labelGroup.LabelGroupName, value, serviceName);
        }

        if (resolved.Count == 0)
        {
            return;
        }

        item.Matches = (item.Matches ?? [])
            .Where(match => match.LabelGroupName == null || !resolved.ContainsKey(match.LabelGroupName))
            .Concat(resolved.Values)
            .ToList();
    }

    /// <summary>
    /// The v3 meter keys against the shared meter fields.
    ///
    /// The verification evidence is free text, not a verdict ("Flow Meter Verification full report
    /// seen dated 31/03/2025 - meter accuracy 6.33%"), and differs per meter, so it is a string on
    /// the meter rather than a status on the provisions. It fills VerificationCertificate, which
    /// had no extraction wired to it before.
    /// </summary>
    private static readonly (string Key, string FieldName)[] MeterKeys =
    [
        (MeterGroupFirstKey, WrInspectionReportFieldNames.MeterName),
        ("Meter make and model", WrInspectionReportFieldNames.MeterMake),
        ("Serial number", WrInspectionReportFieldNames.SerialNumber),
        ("Meter reading at inspection", WrInspectionReportFieldNames.Reading),
        ("Unit of measurement", WrInspectionReportFieldNames.Units),
        ("Metering accuracy, calibration or verification evidence reviewed",
            WrInspectionReportFieldNames.VerificationCertificate),
        ("Maintenance, servicing or inspection records reviewed",
            WrInspectionReportFieldNames.MaintenanceRecords)
    ];

    /// <summary>
    /// One line per meter into the shared meter fields, which
    /// WrInspectionReportSchemaConverter.BuildMeters then zips by line index into one record each.
    ///
    /// An absent value contributes an empty line to hold its position, but that does not survive:
    /// GetMultilineTextLines drops blank lines, so a gap still shifts every later meter up. The
    /// samples hide it because their only sparse field (maintenance records) has its value on the
    /// first meter. A value absent on an earlier meter and present on a later one would attach to
    /// the wrong borehole. Fixing it means preserving interior blanks in that read, which also
    /// changes v1 meter counts, so it is not a one-line change.
    /// </summary>
    public static void ApplyMeters(
        MatchesResult item,
        IReadOnlyList<DocumentTable> tables,
        string serviceName)
    {
        var meters = KeyValueTableHelper.ReadRepeatingGroups(tables, MeterGroupFirstKey);

        if (meters.Count == 0)
        {
            return;
        }

        var resolved = new Dictionary<string, LabelGroupResult>();

        foreach (var (key, fieldName) in MeterKeys)
        {
            var perMeter = meters
                .Select(meter => KeyValueTableHelper.FindValue(meter, key) ?? string.Empty)
                .ToList();

            if (perMeter.All(string.IsNullOrWhiteSpace))
            {
                continue;
            }

            resolved[fieldName] = BuildResultFromLines(fieldName, perMeter, serviceName);
        }

        if (resolved.Count == 0)
        {
            return;
        }

        item.Matches = (item.Matches ?? [])
            .Where(match => match.LabelGroupName == null || !resolved.ContainsKey(match.LabelGroupName))
            .Concat(resolved.Values)
            .ToList();
    }

    private static LabelGroupResult BuildResult(string fieldName, string value, string serviceName)
    {
        var lines = value
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        return BuildResultFromLines(fieldName, lines, serviceName);
    }

    /// <summary>
    /// Keeps empty lines, unlike <see cref="BuildResult"/>: in a repeating group the line position
    /// carries meaning. Note that the converter's own read then drops them - see
    /// <see cref="ApplyMeters"/>.
    /// </summary>
    private static LabelGroupResult BuildResultFromLines(
        string fieldName,
        IReadOnlyList<string> values,
        string serviceName)
    {
        var lines = values
            .Select(line => new DocumentLine
            {
                Columns = [new DocumentLineColumn(DocumentLineColumn.TextToWords(line.Trim(), null))]
            })
            .ToList();

        // MatchedLabelName as well as LabelGroupName: WrInspectionReportSchemaConverter
        // .GetMultilineText resolves a field by MatchedLabelName, so a result carrying only the
        // group name reads back as empty even though it is present.
        return new LabelGroupResult
        {
            LabelGroupName = fieldName,
            MatchedLabelName = fieldName,
            ServiceName = serviceName,
            Text = lines
        };
    }
}
