using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Methods;

namespace WRADI.DocumentType.WrInspectionReport.Services;

// Additive, fallback-safe overlay on top of the heuristic column-walk matching
// (WALE.ProcessFile.Services/Helpers/FindLabelGroupMatchesHelper.cs) for the LicenceProvisions
// grid fields. Given real table cells from Azure AI Document Intelligence's "prebuilt-layout"
// model (see AzureAiServicesDocumentIntelligenceTableExtractorService), looks up each grid
// field's answer by real RowIndex/ColumnIndex adjacency instead of position-heuristic guessing.
// Only returns a result for fields it can confidently resolve - the caller (
// WrInspectionReportExtractionOrchestrator) keeps the existing heuristic result for anything
// not present in the returned dictionary, so this can never make an already-correct field
// worse, only replace an already-wrong one.
public static class WrInspectionReportTableMatcher
{
    public static Dictionary<string, LabelGroupResult> MatchGridFields(
        IReadOnlyList<OcrTable> tables,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        IReadOnlyList<string> gridFieldNames,
        string serviceName)
    {
        var results = new Dictionary<string, LabelGroupResult>();

        var bestTable = FindBestGridTable(tables, labelLookups, gridFieldNames);

        if (bestTable == null)
        {
            return results;
        }

        foreach (var fieldName in gridFieldNames)
        {
            var label = labelLookups
                .FirstOrDefault(l => l.LabelGroupName == fieldName).Labels?.FirstOrDefault();

            if (label?.TextStart == null || label.Possibilities == null)
            {
                continue;
            }

            var rawCellText = FindFieldValueInTable(bestTable, label.TextStart);

            if (rawCellText == null)
            {
                continue; // Field's label wasn't found in this table at all - fall back to the heuristic.
            }

            var matchedPossibility = label.Possibilities
                .FirstOrDefault(possibility => BaseMethod.MatchesPossibility(rawCellText, possibility));

            if (matchedPossibility == null)
            {
                continue; // Cell content doesn't match any known answer shape - fall back rather than guess.
            }

            var words = matchedPossibility.Text.Length == 0
                ? []
                : DocumentLineColumn.TextToWords(matchedPossibility.Text, null);

            var syntheticLine = new DocumentLine
            {
                Columns = [new DocumentLineColumn(words)]
            };

            results[fieldName] = new LabelGroupResult
            {
                LabelGroupName = fieldName,
                MatchedLabelName = fieldName,
                ServiceName = serviceName,
                Text = [syntheticLine]
            };
        }

        return results;
    }

    // The table whose cells collectively match the most grid field labels - mirrors the
    // scoring approach validated during prototyping (best_ti/best_count) against 17 real
    // documents. A document can have several tables (header block, meter block, etc.); this
    // picks out the LicenceProvisions grid specifically rather than assuming table order.
    private static OcrTable? FindBestGridTable(
        IReadOnlyList<OcrTable> tables,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        IReadOnlyList<string> gridFieldNames)
    {
        OcrTable? bestTable = null;
        var bestCount = 0;

        foreach (var table in tables)
        {
            var matchedFieldCount = gridFieldNames.Count(fieldName =>
            {
                var label = labelLookups
                    .FirstOrDefault(l => l.LabelGroupName == fieldName).Labels?.FirstOrDefault();

                return label?.TextStart != null
                    && table.Cells.Any(cell => CellStartsWithAnyLabel(cell, label.TextStart));
            });

            if (matchedFieldCount > bestCount)
            {
                bestCount = matchedFieldCount;
                bestTable = table;
            }
        }

        // Require a meaningful majority of the grid before trusting this table at all - a
        // table that only happens to contain one or two grid label texts (e.g. a coincidental
        // overlap with an unrelated block) isn't a reliable source for the rest of the fields.
        return bestCount >= gridFieldNames.Count / 2 ? bestTable : null;
    }

    private static bool CellStartsWithAnyLabel(OcrTableCell cell, IReadOnlyList<TextToMatch> textStarts)
    {
        return cell.Content != null
            && textStarts.Any(textStart => cell.Content.StartsWith(textStart.Text, StringComparison.OrdinalIgnoreCase));
    }

    // A real tick/status answer ("✓", "N/A", "NI", "☑ ☐", etc. - see GetInOrderField's
    // Possibilities list) is always a handful of characters once Azure's own ":selected:"/
    // ":unselected:" annotation is stripped back out. Real narrative-answer documents (the
    // "water_company_template"/"narrative_provisions" family - see wr51_groundtruth_labelling
    // memory) put a full sentence in the same cell instead, e.g. "Source of supply: Lower
    // Greensand at Warwick Wold / Brewer St :selected:" - confirmed via a real golden-set
    // harness run this genuinely happens, not a theoretical edge case. Generous headroom above
    // the longest real Possibility ("☑ ☐", 3 chars) while still excluding any real sentence.
    private const int MaxTickAnswerLength = 8;

    // Handles both cell shapes found during prototyping: (a) label and value merged into one
    // cell (majority of documents - strip the label prefix, remainder is the answer), and
    // (b) label and value split into adjacent cells (e.g. wr51__nw0690016005 - when the label
    // cell's own remainder is empty, the answer is the next cell in the same row).
    private static string? FindFieldValueInTable(OcrTable table, IReadOnlyList<TextToMatch> textStarts)
    {
        foreach (var cell in table.Cells)
        {
            if (cell.Content == null)
            {
                continue;
            }

            var matchedTextStart = textStarts
                .FirstOrDefault(textStart =>
                    cell.Content.StartsWith(textStart.Text, StringComparison.OrdinalIgnoreCase));

            if (matchedTextStart == null)
            {
                continue;
            }

            // A bare leftover colon (the label's own punctuation, not part of the answer) must
            // not be mistaken for a real merged-cell answer - otherwise the split-cell case
            // below (label alone in its cell, e.g. "Special conditions:") never triggers and
            // the real answer in the next cell is missed entirely. Checked against the RAW
            // (not selection-mark-normalised) content - normalising first, then checking length,
            // would already have turned ":selected:" into "✓" and hidden how long the rest of a
            // narrative sentence actually is.
            var rawRemainder = cell.Content[matchedTextStart.Text.Length..].Trim().TrimStart(':').Trim();

            if (rawRemainder.Length > 0)
            {
                // Found the field's own label, but its content isn't tick-shaped - a genuine
                // narrative answer, which the current model has no way to represent anyway (see
                // WrInspectionReportSchemaConverter.GetInOrderStatus). Don't fabricate a status
                // by matching a stray "in"/"n"/"✓"-after-normalising substring somewhere inside
                // real prose - fall back to the heuristic (which has the exact same
                // representational limit, but at least won't confidently guess) rather than
                // returning something here at all.
                return LooksLikeATickAnswer(rawRemainder) ? NormaliseSelectionMarks(rawRemainder) : null;
            }

            var nextCell = table.Cells.FirstOrDefault(c =>
                c.RowIndex == cell.RowIndex && c.ColumnIndex == cell.ColumnIndex + 1);

            if (nextCell?.Content == null)
            {
                return string.Empty;
            }

            var nextRaw = nextCell.Content.Trim();
            return LooksLikeATickAnswer(nextRaw) ? NormaliseSelectionMarks(nextRaw) : null;
        }

        return null;
    }

    private static bool LooksLikeATickAnswer(string rawText)
    {
        var withoutSelectionMarks = rawText
            .Replace(":selected:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(":unselected:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        return withoutSelectionMarks.Length <= MaxTickAnswerLength;
    }

    private static string NormaliseSelectionMarks(string content) => content
        .Replace(":selected:", "✓", StringComparison.OrdinalIgnoreCase)
        .Replace(":unselected:", string.Empty, StringComparison.OrdinalIgnoreCase);
}
