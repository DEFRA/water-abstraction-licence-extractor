using System.Text;
using Tabula;
using Tabula.Detectors;
using Tabula.Extractors;
using UglyToad.PdfPig;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Tabula;

// Reads tables directly off the PDF's own text/vector layer via PdfPig (the same library
// already used elsewhere in this pipeline for native-text extraction) - no OCR, no cloud call,
// no per-document cost, works entirely offline. The trade-off: it only sees anything at all on
// pages that have a real text layer. A genuinely scanned page (no text/vector content at all)
// yields nothing here and still needs Azure DI/OCR.
//
// Spot-checked against 4 real WR51 documents (2 baseline/T1, 1 water_company_template/T4, 1
// multi_meter_table/T6) before building this - Lattice mode (SpreadsheetExtractionAlgorithm)
// cleanly extracted the LicenceProvisions grid, the Maintenance/Readings-taken sub-cells, and
// the multi-meter table on all 4. See the wr51_textract_tables_design memory for the full
// evidence; this class is the first real (not scratchpad) implementation, wired through the
// existing ITableExtractorService/OcrTable abstraction with zero change needed to
// WrInspectionReportTableMatcher.
//
// Runs BOTH algorithms per page and returns every candidate table from either - not "try Lattice,
// fall back to Stream only if Lattice found nothing". WrInspectionReportTableMatcher.
// FindBestGridTable already scores every candidate by how many grid field labels it actually
// contains and picks the highest-scoring one past a majority threshold, so handing it more
// candidates only ever helps: a genuine Lattice table (real ruled borders) still wins outright
// against Stream's coarser output whenever both are present, and Stream is the only one of the
// two that can find anything at all on documents with no ruled borders around the grid at all
// (e.g. wr51__114222355, confirmed via the truth-set harness run that motivated adding this -
// each LicenceProvisions field sits on its own paragraph line there, no drawn table).
// SimpleNurminenDetectionAlgorithm (region detection) + BasicExtractionAlgorithm (the actual cell
// split) is the Stream-mode pairing tabula-sharp's own README documents.
//
// No caching - unlike the cloud-based implementations of this interface, there's no API cost to
// avoid on a repeat call, only CPU time, which is fast (local PDF parsing, no network round trip).
public class TabulaTableExtractorService : ITableExtractorService
{
    public string Name => "TabulaSharp";

    public Task<IReadOnlyList<DocumentTable>> GetTablesAsync(
        byte[] documentBytes,
        Guid fileId,
        int processRunId)
    {
        using var document = UglyToad.PdfPig.PdfDocument.Open(
            documentBytes,
            new ParsingOptions { ClipPaths = true });

        var tables = new List<DocumentTable>();
        var latticeAlgorithm = new SpreadsheetExtractionAlgorithm();
        var streamDetector = new SimpleNurminenDetectionAlgorithm();
        var streamAlgorithm = new BasicExtractionAlgorithm();

        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = ObjectExtractor.Extract(document, pageNumber);

            // Two independent algorithms against the same page - a crash in one (a PDF's own
            // malformed content confusing one algorithm's assumptions, not the other's) mustn't
            // lose the other's otherwise-good result for this page.
            try
            {
                tables.AddRange(latticeAlgorithm
                    .Extract(page)
                    .Select(table => ToOcrTable(table, pageNumber)));
            }
            catch (Exception)
            {
                // Stream mode below runs unconditionally either way, on a success or a failure
                // here - this catch only stops a Lattice crash from also losing whatever Stream
                // still finds for this page. It is not a fallback triggered by this failure.

                // TODO log
            }

            try
            {
                foreach (var region in streamDetector.Detect(page))
                {
                    tables.AddRange(streamAlgorithm
                        .Extract(page.GetArea(region.BoundingBox))
                        .Select(table => ToOcrTable(table, pageNumber)));
                }
            }
            catch (Exception)
            {
                // Lattice's results for this page (if any) are still returned.
                
                // TODO log
            }
        }

        return Task.FromResult<IReadOnlyList<DocumentTable>>(tables);
    }

    private static DocumentTable ToOcrTable(Table table, int pageNumber)
    {
        var cells = new List<DocumentTableCell>();

        for (var rowIndex = 0; rowIndex < table.Rows.Count; rowIndex++)
        {
            var row = table.Rows[rowIndex];

            cells.AddRange(row.Select((cell, columnIndex) =>
                new DocumentTableCell
                {
                    RowIndex = rowIndex,
                    ColumnIndex = columnIndex,
                    Content = GetCellText(cell),
                    Left = cell.Left,
                    Top = cell.Top
                }));
        }

        return new DocumentTable
        {
            PageNumber = pageNumber,
            RowCount = table.RowCount,
            ColumnCount = table.ColumnCount,
            Cells = cells
        };
    }

    // Cell.GetText() sometimes drops the space between two of its own TextChunks entirely,
    // confirmed on a real file (wr51__sw0480192006): "Means of abstraction: n/a" and "Records:
    // n/a" sit 48pt apart yet GetText() joins them as "...n/aRecords: n/a..." with zero
    // separator. Real, previously-unreported defect specific to Stream mode's coarser
    // per-region cells (Lattice mode's own tightly-bounded single-field cells never hit this,
    // since they typically contain only one TextChunk to begin with, nothing to join).
    //
    // Two chunks on the same visual line can differ in Top by over a point (confirmed: 519.01 vs
    // 520.22 for "Purpose(s)" and "Provision of information", genuinely the same row) - a naive
    // sort on exact (or even rounded-to-the-point) Top treats that as "different line" and
    // reverses left/right order. GroupIntoLines clusters chunks whose Top falls within
    // LineGroupingTolerance of the line's own first (topmost) member, rather than aligning to a
    // fixed grid - close enough together to be the same row, however their own Top jitters,
    // while still separating genuinely different rows (this corpus's own row heights run 6-10pt,
    // several times the tolerance).
    private const double LineGroupingTolerance = 2.5;

    // First version of this fix always inserted a space between TextChunks - wrong on at least
    // one real document, where Cell.TextElements turned out to be GLYPH-level, not word-level
    // ("Licence serial" rendered as one TextChunk per letter): every letter gained its own space
    // ("L i c e n c e s e r i a l"). A second version compared each gap against a fraction of
    // the line's own letter height - better, but still a guess: on a real file (the Drax
    // licence PDF) that font's actual letter-to-letter AND word-to-word gaps both came out
    // smaller than the height-based threshold, so it swung to the opposite failure (nothing
    // spaced at all: "LicenceSerialNo: Pleasequotetheserialnumber...").
    //
    // Fixed properly by dropping to the glyph level unconditionally (TextChunk.TextElements -
    // regardless of whether this document's own TextChunks came out word- or letter-granularity,
    // every TextChunk still decomposes to its own individual TextElements) and using each
    // TextElement's own WidthOfSpace - PdfPig's actual computed space-glyph width for that font
    // at that point - as the threshold, instead of approximating one from letter width. This is
    // the same signal a real word extractor would use, not a proxy for it.
    private const double SpaceGapTolerance = 0.5;

    // Joining lines with '\n' (rather than a space) seemed like a nicer representation of a
    // multi-field merged cell, but regressed real ground-truth accuracy on 3 grid fields
    // (Land/OtherProvisions/ChargingFactors, confirmed via the harness both ways) - something
    // downstream treats an embedded newline differently from a plain space even though nothing
    // in this codebase's own field matching should care. Not worth chasing down for a cosmetic
    // improvement; a single space between lines is what was already verified end-to-end.
    private static string GetCellText(Cell cell)
    {
        return string.Join(" ", GroupIntoLines(cell.TextElements).Select(BuildLineText).Where(text => text.Length > 0)).Trim();
    }

    private static string BuildLineText(List<TextChunk> line)
    {
        // Chunk order within a line isn't guaranteed by GroupIntoLines (it only clusters by Top
        // tolerance) - without this, two TextChunks landing on the same line in the wrong
        // relative order silently reverses which field reads first (confirmed on a real file:
        // "Purpose(s)" and "Provision of information" swapped without it).
        var letters = line
            .OrderBy(chunk => chunk.Left)
            .SelectMany(chunk => chunk.TextElements.OrderBy(letter => letter.Left))
            .Where(letter => !string.IsNullOrWhiteSpace(letter.Letter.Value))
            .ToList();

        var sb = new StringBuilder();
        TextElement? previous = null;

        foreach (var letter in letters)
        {
            if (previous != null)
            {
                var gap = letter.Left - previous.Right;
                var threshold = Math.Max(previous.WidthOfSpace, letter.WidthOfSpace) * SpaceGapTolerance;

                if (gap > threshold)
                {
                    sb.Append(' ');
                }
            }

            sb.Append(letter.Letter.Value);
            previous = letter;
        }

        return sb.ToString().Trim();
    }

    private static List<List<TextChunk>> GroupIntoLines(IReadOnlyList<TextChunk> chunks)
    {
        var lines = new List<List<TextChunk>>();

        // PDF space is Y-up; descending Top visits chunks top-to-bottom, matching reading order.
        foreach (var chunk in chunks.OrderByDescending(chunk => chunk.Top))
        {
            var currentLine = lines.Count > 0 ? lines[^1] : null;

            if (currentLine != null && currentLine[0].Top - chunk.Top <= LineGroupingTolerance)
            {
                currentLine.Add(chunk);
            }
            else
            {
                lines.Add([chunk]);
            }
        }

        return lines;
    }
}