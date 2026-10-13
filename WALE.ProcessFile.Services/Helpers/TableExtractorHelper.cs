using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Methods;

namespace WALE.ProcessFile.Services.Helpers;

// Overlay on FindLabelGroupMatchesHelper's heuristic column-walk for the LicenceProvisions grid
// fields: given real table cells, resolves each field by RowIndex/ColumnIndex adjacency instead
// of position guessing. Only returns fields it can resolve confidently - the caller
// (WrInspectionReportExtractionOrchestrator) keeps the heuristic result for anything absent.
public static class TableMatcherHelper
{
    public static Dictionary<string, LabelGroupResult> MatchToPossibilities(
        IReadOnlyList<DocumentTable> tables,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        string serviceName,
        Func<string?, string?>? transformContentFunction,
        // Optional - see FindTextValueInTable. Stops a blank field taking a neighbouring
        // field's label as its value.
        IReadOnlyList<string>? siblingLabels = null)
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
                    var matchedContent = FindTextValueInTable(table, label.TextStart, siblingLabels);

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
        string serviceName,
        // Optional - see FindTextValueInTable. Only matters where the adjacent-cell fallback
        // can land on a sibling field's label.
        IReadOnlyList<string>? siblingLabels = null)
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
                    rawValue = FindTextValueInTable(table, label.TextStart, siblingLabels);

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
    /// For fields that genuinely repeat per row (a document with several meters).
    /// MatchTextFields returns only the FIRST matching cell, silently keeping one meter at random
    /// and discarding the rest, while the converter's BuildMeters expects one Text line per meter.
    /// Matches the label anywhere in a cell, not just leading - real documents have "Meter make:"
    /// mid-cell - bounds each value at the nearest boundaryLabels term so merged cells don't bleed
    /// into each other, and returns one line per cell in row order (which is meter order).
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

                // PageNumber before RowIndex: RowIndex restarts at 0 per table, so ordering by it
                // alone lets a page 2 meter sort above a page 1 one, scrambling which meter's
                // make/serial/reading get zipped together in BuildMeters.
                values = tables
                    .SelectMany(table => FindAllTextValuesInTable(table, label.TextStart, boundaryLabels)
                        .Select(v => (table.PageNumber, v.RowIndex, v.Value)))
                    .OrderBy(v => v.PageNumber)
                    .ThenBy(v => v.RowIndex)
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

    // A match longer than this means the label occurrence was coincidental, inside an unrelated
    // paragraph with no boundary term to stop it. Headroom over the longest genuine golden-set
    // value ("Not available during site visit", ~35 chars), well short of a real sentence.
    private const int MaxPlausibleValueLength = 60;

    // Every occurrence of textToMatch, not just the first - see MatchMultiValueTextFields. A value
    // ends at the nearest boundaryLabels term in the same cell, or the cell's end. Two golden-set
    // failure modes drove the guards: a label inside a longer word ("serial numbers" in a caption
    // matching "serial number", leaving a stray "s"), rejected by a word-boundary check either side
    // as in BaseMethod.MatchesPossibility's ExceptWhenInsideWord; and a label whose value sits in
    // the adjacent cell instead, handled by the same fallback FindTextValueInTable uses.
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

                    // A blank field sits next to a sibling's label cell more often than next to a
                    // real value for itself, so without this the sibling gets taken as the value.
                    // Rejects any next cell STARTING with a known label, not just a bare one - a
                    // populated sibling cell reads just as plausibly as a value.
                    var nextCellIsASiblingLabel = nextCell?.Content != null &&
                        boundaryLabels.Any(b => FindWordBoundedIndex(nextCell.Content, b) == 0);

                    if (!nextCellIsASiblingLabel &&
                        !string.IsNullOrWhiteSpace(nextCell?.Content) &&
                        nextCell.Content!.Length <= MaxPlausibleValueLength)
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

                // No boundary term and a long remainder is the tell that the label match was
                // coincidental, inside a narrative paragraph - dropped below rather than trusted.
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
    /// <param name="siblingLabels">
    /// Optional - see MatchMultiValueTextFields/FindAllTextValuesInTable's own doc comment for
    /// the underlying failure mode. When a field's own value is genuinely blank (e.g. "Meter
    /// make:" with nothing filled in), both cell shapes above can otherwise return a sibling
    /// field's own label - either merged into the same cell right after this field's label
    /// ("Meter make: Serial number: 96280940"), or as the next cell's entire content ("Serial
    /// number:" / "Serial number: 96280940" in the adjacent cell). Passing the sibling field
    /// labels here bounds the merged case at the first one found and rejects the adjacent-cell
    /// case outright when it starts with one. Left null for fields with no known siblings to
    /// guard against, keeping their behaviour exactly as before.
    /// </param>
    /// <returns></returns>
    private static string? FindTextValueInTable(
        DocumentTable table, IReadOnlyList<TextToMatch> textToMatch, IReadOnlyList<string>? siblingLabels = null)
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
                if (siblingLabels != null)
                {
                    var boundaryIndex = siblingLabels
                        .Select(b => rawRemainder.IndexOf(b, StringComparison.OrdinalIgnoreCase))
                        .Where(i => i >= 0)
                        .DefaultIfEmpty(-1)
                        .Min();

                    if (boundaryIndex == 0)
                    {
                        return string.Empty;
                    }

                    if (boundaryIndex > 0)
                    {
                        return rawRemainder[..boundaryIndex].Trim();
                    }
                }

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

            if (siblingLabels != null &&
                siblingLabels.Any(b => FindWordBoundedIndex(nextCell.Content!, b) == 0))
            {
                return string.Empty;
            }

            return nextCell.Content?.Trim();
        }

        return null;
    }
}