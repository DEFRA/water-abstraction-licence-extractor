using System.Text;
using System.Text.Json;
using Tabula;
using Tabula.Detectors;
using Tabula.Extractors;
using UglyToad.PdfPig;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OcrService;
using PdfDocument = WALE.ProcessFile.Core.Models.PdfDocument;

namespace WALE.ProcessFile.Services.Tabula;

// Reads tables directly off the PDF's own text/vector layer via PdfPig. Only sees anything at all on
// pages that have a real text layer. A genuinely scanned page (no text/vector content at all)
// yields nothing here and still needs OCR.
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
public class TabulaTableExtractorService(ICacheService cacheService) : ITableExtractorService
{
    public string Name => "TabulaSharp";
    private const int SharedPageNumber = 1;

    public async Task<IReadOnlyList<DocumentTable>> GetTablesAsync(
        PdfDocument pdfDocument,
        Guid fileId,
        int processRunId)
    {
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = SharedPageNumber,
            ImageNumber = 0,
            FileId = fileId,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        var cacheText = await cacheService.GetOcrImageTextAsync(request);

        if (!string.IsNullOrEmpty(cacheText))
        {
            var cachedTables = JsonSerializer.Deserialize<List<DocumentTable>>(
                cacheText,
                JsonHelper.GetSerializerOptions());

            return cachedTables!;
        }
        
        // Deliberately reads pdfDocument.Bytes directly rather than going through
        // OpenInternalDocumentAsync() - that path calls out to NoOcrPdfDocumentService/
        // IFileService to fetch bytes by filename, machinery this extractor doesn't need since
        // the bytes are already in hand. It also isn't populated on the ad-hoc PdfDocument
        // WrInspectionReportExtractionOrchestrator.GetTableMatchesAsync constructs for this
        // overlay - confirmed calling OpenInternalDocumentAsync() there throws a
        // NullReferenceException, silently zeroing out this extractor's contribution through
        // that path (see PdfClownGridTableExtractorService for the same fix, found first).
        var bytes = pdfDocument.Bytes;

        if (bytes == null)
        {
            return [];
        }

        using var document = UglyToad.PdfPig.PdfDocument.Open(bytes);

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
            catch (Exception ex)
            {
                // Falls through to Stream mode below for this page; the caller's own try/catch
                // around the whole overlay covers the case where even that isn't enough.
                
                Console.WriteLine($"INFO - {nameof(TabulaTableExtractorService)} - Lattice, exception {ex.Message}, continuing");
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
            catch (Exception ex)
            {
                // Lattice's results for this page (if any) are still returned.
                
                Console.WriteLine($"INFO - {nameof(TabulaTableExtractorService)} - Stream, exception {ex.Message}, continuing");
            }
        }

        var noneEmptyTables = new List<DocumentTable>();
        
        foreach (var table in tables)
        {
            var anyNoneEmptyCells = false;

            foreach (var cell in table.Cells)
            {
                if (!string.IsNullOrEmpty(cell.Content))
                {
                    anyNoneEmptyCells = true;
                    break;
                }
            }

            if (anyNoneEmptyCells)
            {
                noneEmptyTables.Add(table);
            }
        }
        
        tables = noneEmptyTables;
        
        await cacheService.SaveOcrImageTextAsync(
            request,
            JsonSerializer.Serialize(tables, JsonHelper.GetSerializerOptions()));
        
        return tables;
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
                    Content = GetCellText(cell).Trim(),
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
    
    private static string GetCellText(Cell cell)
    {
        var lines = GroupIntoLines(cell.TextElements);
        var outputTextSb = new StringBuilder();
        
        foreach (var line in lines)
        {
            // Sorting each chunk's own letters and relying on the chunks' insertion order isn't
            // enough - GroupIntoLines only orders by Top (grouping into rows), so two chunks on
            // the same row aren't guaranteed to already be left-to-right, and if they overlap in
            // X a per-chunk sort can't fix that anyway. Flatten first, then sort every letter on
            // the line globally by its own Left.
            var letters = line
                .SelectMany(word => word.TextElements)
                .Where(letter => !string.IsNullOrWhiteSpace(letter.Letter.Value))
                .OrderBy(letter => letter.Left)
                .ToList();

            TextElement? previousLetter = null;
            const double spaceGapTolerance = 0.5;

            foreach (var letter in letters)
            {
                if (previousLetter != null)
                {
                    var previousLetterRight = previousLetter.Left + previousLetter.Letter.Width;
                    var gap = letter.Left - previousLetterRight;
                    var threshold = Math.Max(previousLetter.WidthOfSpace, letter.WidthOfSpace) * spaceGapTolerance;

                    if (gap > threshold)
                    {
                        outputTextSb.Append(' ');
                    }
                }

                outputTextSb.Append(letter.Letter.Value);
                previousLetter = letter;
            }
            
            outputTextSb.Append('\n');
        }

        return outputTextSb.ToString().Trim();
    }
    
    private static List<List<TextChunk>> GroupIntoLines(IReadOnlyList<TextChunk> wordsOrChunks)
    {
        var lines = new List<List<TextChunk>>();
        const double lineGroupingTolerance = 2.5;
        
        // PDF space is Y-up (0 at bottom of the document)
        foreach (var wordOrChunk in wordsOrChunks.OrderByDescending(chunk => chunk.Top))
        {
            var currentLine = lines.Count > 0 ? lines[^1] : null;

            if (currentLine != null && currentLine[0].Top - wordOrChunk.Top <= lineGroupingTolerance)
            {
                currentLine.Add(wordOrChunk);
            }
            else
            {
                lines.Add([wordOrChunk]);
            }
        }

        return lines;
    }
}