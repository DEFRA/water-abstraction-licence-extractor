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

            var rawRemainder = cell.Content[matchedTextToMatch.Text.Length..].Trim().TrimStart(':').Trim();

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