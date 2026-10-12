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
    private static LabelGroupResult BuildResult(string fieldName, string value, string serviceName)
    {
        var lines = value
            .ReplaceLineEndings("\n")
            .Split('\n', StringSplitOptions.RemoveEmptyEntries)
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
