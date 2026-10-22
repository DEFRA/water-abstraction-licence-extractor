using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;
using WRADI.DocumentType.WrInspectionReport.Constants;

namespace WRADI.DocumentType.WrInspectionReport.Services;

/// <summary>
/// Overlays the v3 form's two-column key/value tables onto the label-rule results. A field it
/// cannot resolve keeps what the rules found, so this can only add, never take one away.
///
/// A rule opts in with FromLetterAndKeyValueTable(), and its own start text is the lookup key.
/// </summary>
public static class WrInspectionV3KeyValueMatcher
{
    public const string MeterGroupFirstKey = "Meter location or reference";

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
                .FirstOrDefault(found => !string.IsNullOrWhiteSpace(found?.Text));

            if (value == null)
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

    /// The verification evidence is free text differing per meter ("meter accuracy 6.33%"), not a
    /// verdict, so it fills the meter's VerificationCertificate rather than a provision status.
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
    /// One line per meter, zipped by line index in BuildMeters. An absent value contributes an
    /// empty line to hold its position and the converter's v3 read preserves interior blanks -
    /// without both halves, a meter with a gap pulls the next meter's serial number onto itself.
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
                .Select(meter => KeyValueTableHelper.FindValue(meter, key) ?? Blank)
                .ToList();

            if (perMeter.All(meter => string.IsNullOrWhiteSpace(meter.Text)))
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

    private static readonly string[] ActionHeaders = ["Reference", "Type", "Action", "Complete by"];

    private const string UnfilledDropdown = "Choose an item.";

    private static readonly (string Header, string FieldName)[] ActionKeys =
    [
        ("Reference", WrInspectionReportFieldNames.ActionReference),
        ("Type", WrInspectionReportFieldNames.ActionType),
        ("Action", WrInspectionReportFieldNames.ActionDescription),
        ("Complete by", WrInspectionReportFieldNames.ActionCompleteBy)
    ];

    /// One line per action into four parallel fields, zipped by index as the meters are.
    public static void ApplyActions(
        MatchesResult item,
        IReadOnlyList<DocumentTable> tables,
        string serviceName)
    {
        var actions = HeaderedTableHelper.ReadRows(tables, ActionHeaders, [UnfilledDropdown]);

        if (actions.Count == 0)
        {
            return;
        }

        var resolved = new Dictionary<string, LabelGroupResult>();

        foreach (var (header, fieldName) in ActionKeys)
        {
            var perAction = actions
                .Select(action => action.GetValueOrDefault(header, Blank))
                .Select(value => fieldName == WrInspectionReportFieldNames.ActionType
                    ? value with { Text = RejoinHyphenatedWrap(value.Text) }
                    : value)
                .ToList();

            if (perAction.All(action => string.IsNullOrWhiteSpace(action.Text)))
            {
                continue;
            }

            resolved[fieldName] = BuildResultFromLines(fieldName, perAction, serviceName);
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

    /// The type cell wraps mid-word, giving "Non- compliance". Applied to the type only, a dropdown
    /// whose values never hold a space after a hyphen; in the action text that can be genuine.
    private static string RejoinHyphenatedWrap(string value) => value.Replace("- ", "-");

    private static readonly DocumentTableValue Blank = new(string.Empty, null);

    /// Every line of a wrapped cell carries the whole cell's region; no table extractor reports a
    /// per-line rectangle to split it by.
    private static LabelGroupResult BuildResult(
        string fieldName,
        DocumentTableValue value,
        string serviceName)
    {
        var lines = value.Text
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Select(line => value with { Text = line })
            .ToList();

        return BuildResultFromLines(fieldName, lines, serviceName);
    }

    /// Keeps empty lines, unlike <see cref="BuildResult"/>.
    private static LabelGroupResult BuildResultFromLines(
        string fieldName,
        IReadOnlyList<DocumentTableValue> values,
        string serviceName)
    {
        var lines = values
            .Select(line => new DocumentLine
            {
                Columns =
                [
                    new DocumentLineColumn(
                        DocumentLineColumn.TextToWords(line.Text.Trim(), null, line.Coordinates))
                ]
            })
            .ToList();

        // MatchedLabelName as well: GetMultilineText resolves by it, so a result carrying only the
        // group name reads back as empty.
        return new LabelGroupResult
        {
            LabelGroupName = fieldName,
            MatchedLabelName = fieldName,
            ServiceName = serviceName,
            Text = lines
        };
    }
}
