using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Methods;

namespace WALE.ProcessFile.Services.Helpers;

// Overlay on top of the heuristic column-walk matching (WALE.ProcessFile.Services/Helpers/
// FindLabelGroupMatchesHelper.cs) for the LicenceProvisions grid fields. Given real table cells
// from Azure AI Document Intelligence's "prebuilt-layout" model, looks up each grid field's
// answer by RowIndex/ColumnIndex adjacency instead of position-heuristic guessing. Only returns
// a result for fields it can confidently resolve - the caller (
// WrInspectionReportExtractionOrchestrator) keeps the existing heuristic result for anything not
// present in the returned dictionary.
public static class TableMatcherHelper
{
    public static Dictionary<string, LabelGroupResult> MatchToPossibilities(
        IReadOnlyList<DocumentTable> tables,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        string serviceName,
        Func<string?, string?>? transformContentFunction)
    {
        var results = new Dictionary<string, LabelGroupResult>();
        
        foreach (var labelGroup in labelLookups)
        {
            foreach (var label in labelGroup.Labels)
            {
                if (label.TextStart == null || label.Possibilities == null)
                {
                    continue;
                }

                foreach (var table in tables)
                {
                    var matchedContent = FindTextValueInTable(table, label.TextStart);

                    if (transformContentFunction != null)
                    {
                        matchedContent = transformContentFunction(matchedContent);
                    }

                    if (matchedContent == null)
                    {
                        continue;
                    }

                    var matchedPossibility = label.Possibilities
                        .FirstOrDefault(possibility =>
                            BaseMethod.MatchesPossibility(matchedContent, possibility));

                    if (matchedPossibility == null)
                    {
                        continue;
                    }

                    var words = matchedPossibility.Text.Length == 0
                        ? []
                        : DocumentLineColumn.TextToWords(matchedPossibility.Text, null);

                    var syntheticLine = new DocumentLine
                    {
                        Columns = [new DocumentLineColumn(words)]
                    };

                    results[labelGroup.LabelGroupName] = new LabelGroupResult
                    {
                        LabelGroupName = labelGroup.LabelGroupName,
                        MatchedLabelName = label.Name,
                        ServiceName = serviceName,
                        Text = [syntheticLine]
                    };
                }
            }
        }

        return results;
    }

    public static Dictionary<string, LabelGroupResult> MatchTextFields(
        IReadOnlyList<DocumentTable> tables,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        string serviceName)
    {
        var results = new Dictionary<string, LabelGroupResult>();

        var filteredLabelLookups = labelLookups
            .Where(labelGroup => labelGroup.Labels
                .Any(l => l.LayoutExtractorTableLookupType is LayoutExtractorTableLookupType.FreeText))
            .ToList();

        foreach (var labelGroup in filteredLabelLookups)
        {
            var labels = labelGroup.Labels;

            string? rawValue = null;
            LabelToMatch? matchedLabel = null;

            foreach (var label in labels)
            {
                if (label.TextStart == null)
                {
                    continue;
                }

                foreach (var table in tables)
                {
                    rawValue = FindTextValueInTable(table, label.TextStart);

                    if (!string.IsNullOrEmpty(rawValue))
                    {
                        matchedLabel = label;
                        break;
                    }
                }
                
                if (!string.IsNullOrEmpty(rawValue))
                {
                    break;
                }
            }

            if (string.IsNullOrEmpty(rawValue))
            {
                continue;
            }

            var words = DocumentLineColumn.TextToWords(rawValue, null);
            var syntheticLine = new DocumentLine
            {
                Columns = [new DocumentLineColumn(words)]
            };

            results[matchedLabel!.Name!] = new LabelGroupResult
            {
                LabelGroupName = labelGroup.LabelGroupName,
                MatchedLabelName = matchedLabel.Name,
                ServiceName = serviceName,
                Text = [syntheticLine]
            };
        }

        return results;
    }

    /// <summary>
    /// For fields that can genuinely repeat per row in the same table (a document with several
    /// meters, each its own row/cell) - MatchTextFields/FindTextValueInTable only ever return the
    /// FIRST matching cell, which silently picks one meter's value at random (whichever cell
    /// happened to be first in DocumentTable.Cells) and discards the rest; the schema converter's
    /// own BuildMeters expects one LabelGroupResult.Text line per meter (see
    /// WrInspectionReportSchemaConverter.GetMultilineTextLines), a shape this never produced
    /// because MatchTextFields always returns exactly one line. Finds every cell containing the
    /// label (not just a leading match - a real WR51 document has "Meter make:" appear mid-cell,
    /// e.g. "Meter at previous site visit 25th March 2025 Meter make: ARAD Serial number:
    /// ..."), bounds each value at the nearest sibling field's own label (boundaryLabels) so one
    /// merged cell's several label+value pairs don't bleed into each other, and returns one line
    /// per cell in row order (top to bottom matches the document's own meter ordering).
    /// </summary>
    public static Dictionary<string, LabelGroupResult> MatchMultiValueTextFields(
        IReadOnlyList<DocumentTable> tables,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        string serviceName,
        IReadOnlyList<string> boundaryLabels)
    {
        var results = new Dictionary<string, LabelGroupResult>();

        var filteredLabelLookups = labelLookups
            .Where(labelGroup => labelGroup.Labels
                .Any(l => l.LayoutExtractorTableLookupType is LayoutExtractorTableLookupType.FreeText))
            .ToList();

        foreach (var labelGroup in filteredLabelLookups)
        {
            LabelToMatch? matchedLabel = null;
            List<string> values = [];

            foreach (var label in labelGroup.Labels)
            {
                if (label.TextStart == null)
                {
                    continue;
                }

                values = tables
                    .SelectMany(table => FindAllTextValuesInTable(table, label.TextStart, boundaryLabels))
                    .OrderBy(v => v.RowIndex)
                    .Select(v => v.Value)
                    .ToList();

                if (values.Count > 0)
                {
                    matchedLabel = label;
                    break;
                }
            }

            if (values.Count == 0)
            {
                continue;
            }

            results[matchedLabel!.Name!] = new LabelGroupResult
            {
                LabelGroupName = labelGroup.LabelGroupName,
                MatchedLabelName = matchedLabel.Name,
                ServiceName = serviceName,
                Text = values
                    .Select(value => new DocumentLine
                    {
                        Columns = [new DocumentLineColumn(DocumentLineColumn.TextToWords(value, null))]
                    })
                    .ToList()
            };
        }

        return results;
    }

    // A real meter-detail value (make, serial number, reading, asset number, units) is never
    // this long - a match this size means the "label" occurrence was coincidental, inside an
    // unrelated long-form paragraph (a licence condition or narrative sentence) with no real
    // boundary term to stop it. Generous headroom above the longest genuine value seen in the
    // golden set ("Not available during site visit", ~35 chars) without coming close to a real
    // sentence's length.
    private const int MaxPlausibleValueLength = 60;

    // Every occurrence of any of textToMatch in the table, not just the first - see
    // MatchMultiValueTextFields. A value stops at the nearest occurrence of any boundaryLabels
    // term after it in the same cell (a sibling field's own label bleeding in from the same
    // merged cell), or at the cell's own end when no boundary term appears. Two real-document
    // failure modes found via the golden set drove the two guards here: a label word embedded
    // inside a longer, unrelated word ("serial numbers" in an unrelated photo caption
    // wrongly matching "serial number", leaving just the stray trailing "s" once the match is
    // stripped) - rejected via a word-boundary check on both sides of the match, same approach
    // as BaseMethod.MatchesPossibility's own ExceptWhenInsideWord guard; and a label with no
    // value in the SAME cell at all - the value sits in a separate, adjacent cell instead (the
    // other real WR51 cell shape, already handled by FindTextValueInTable for the single-value
    // case) - same nearest-populated-cell-to-the-right fallback applied here too.
    private static List<(int RowIndex, string Value)> FindAllTextValuesInTable(
        DocumentTable table, IReadOnlyList<TextToMatch> textToMatch, IReadOnlyList<string> boundaryLabels)
    {
        var results = new List<(int RowIndex, string Value)>();

        foreach (var cell in table.Cells)
        {
            if (string.IsNullOrWhiteSpace(cell.Content))
            {
                continue;
            }

            foreach (var textStart in textToMatch)
            {
                var labelIndex = FindWordBoundedIndex(cell.Content, textStart.Text);

                if (labelIndex < 0)
                {
                    continue;
                }

                var afterLabel = cell.Content[(labelIndex + textStart.Text.Length)..]
                    .TrimStart().TrimStart(':', '.').TrimStart();

                if (afterLabel.Length == 0)
                {
                    var nextCell = table.Cells
                        .Where(c => c.RowIndex == cell.RowIndex && c.ColumnIndex > cell.ColumnIndex)
                        .OrderBy(c => c.ColumnIndex)
                        .FirstOrDefault();

                    if (!string.IsNullOrWhiteSpace(nextCell?.Content) && nextCell.Content!.Length <= MaxPlausibleValueLength)
                    {
                        results.Add((cell.RowIndex, nextCell.Content!.Trim()));
                    }

                    break;
                }

                var boundaryIndex = boundaryLabels
                    .Select(b => afterLabel.IndexOf(b, StringComparison.OrdinalIgnoreCase))
                    .Where(i => i >= 0)
                    .DefaultIfEmpty(-1)
                    .Min();

                // No boundary term found (boundaryIndex == -1) and the remainder still runs
                // long is the tell that this cell's "label" occurrence was a coincidental match
                // inside an unrelated long-form sentence (a licence-condition or narrative
                // paragraph, not a real meter-detail cell) - a genuine value never needs this
                // much text, so it's dropped rather than trusted.
                var value = (boundaryIndex >= 0 ? afterLabel[..boundaryIndex] : afterLabel).Trim();

                if (value.Length > 0 && value.Length <= MaxPlausibleValueLength)
                {
                    results.Add((cell.RowIndex, value));
                }

                break;
            }
        }

        return results;
    }

    // The first occurrence of needle in haystack whose match isn't embedded inside a longer
    // word on either side (letter/digit adjacency on both edges rejects it, same rule
    // BaseMethod.MatchesPossibility already uses) - keeps searching past a false-positive
    // occurrence rather than giving up on the whole cell.
    private static int FindWordBoundedIndex(string haystack, string needle)
    {
        var searchStart = 0;

        while (searchStart <= haystack.Length)
        {
            var index = haystack.IndexOf(needle, searchStart, StringComparison.OrdinalIgnoreCase);

            if (index < 0)
            {
                return -1;
            }

            var charBeforeIsLetterOrDigit = index >= 1 && char.IsLetterOrDigit(haystack[index - 1]);
            var charAfterIndex = index + needle.Length;
            var charAfterIsLetterOrDigit = charAfterIndex < haystack.Length && char.IsLetterOrDigit(haystack[charAfterIndex]);

            if (!charBeforeIsLetterOrDigit && !charAfterIsLetterOrDigit)
            {
                return index;
            }

            searchStart = index + 1;
        }

        return -1;
    }

    /// <summary>
    /// Handles both cell shapes seen in practice: (a) label and value merged into one cell (the
    /// majority - strip the label prefix, remainder is the answer), and (b) label and value split
    /// into adjacent cells (label cell's own remainder is empty, answer is the next cell in the
    /// same row).
    /// </summary>
    /// <param name="table"></param>
    /// <param name="textToMatch"></param>
    /// <returns></returns>
    private static string? FindTextValueInTable(DocumentTable table, IReadOnlyList<TextToMatch> textToMatch)
    {
        foreach (var cell in table.Cells)
        {
            if (string.IsNullOrWhiteSpace(cell.Content))
            {
                continue;
            }

            var matchedTextToMatch = textToMatch.FirstOrDefault(textStart =>
                cell.Content.StartsWith(textStart.Text, StringComparison.OrdinalIgnoreCase));

            if (matchedTextToMatch == null)
            {
                continue;
            }

            // TrimStart(':', '.'): a short-form label ("Licence No") that's a strict text
            // prefix of the cell's own longer label ("Licence No.") leaves that trailing
            // punctuation stuck to the front of the remainder once the short prefix is
            // stripped - confirmed via the golden set (LicenceNumber PartialHit on ~15 docs,
            // every one starting ". <the actual, otherwise-correct number>").
            var rawRemainder = cell.Content[matchedTextToMatch.Text.Length..].Trim().TrimStart(':', '.').Trim();

            if (rawRemainder.Length > 0)
            {
                return rawRemainder;
            }

            // The nearest populated cell to the right, not strictly ColumnIndex+1: column
            // indices are assigned per-table, not per-row, so a row whose own real neighbour
            // column has no content anywhere else on the page (or a colspan label skips a
            // column) leaves a gap - PdfClownGridTableExtractorService clusters ColumnIndex from
            // populated-cell positions globally, so a sibling row's different column layout can
            // leave this row's own "next" index unused (confirmed via a real WR51 doc: the
            // Calibration/Conformance/Flow verification/Meter verification row's tick values sit
            // two indices to the right of their own label, not one, because no other row uses the
            // index in between).
            var nextCell = table.Cells
                .Where(c => c.RowIndex == cell.RowIndex && c.ColumnIndex > cell.ColumnIndex)
                .OrderBy(c => c.ColumnIndex)
                .FirstOrDefault();

            if (nextCell?.Content == null)
            {
                return string.Empty;
            }

            return nextCell.Content?.Trim();
        }

        return null;
    }
}