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
// (e.g. wr51__114222355, confirmed via the golden-set harness run that motivated adding this -
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
                // Falls through to Stream mode below for this page; the caller's own try/catch
                // around the whole overlay covers the case where even that isn't enough.
                
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
                    Content = cell.GetText().Trim(),
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
}