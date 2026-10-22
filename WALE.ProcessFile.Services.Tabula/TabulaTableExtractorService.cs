using System.Text;
using UglyToad.PdfPig.Content;
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
// Both algorithms run per page and every candidate is returned, rather than falling back to
// Stream only when Lattice finds nothing: FindBestGridTable already scores candidates by how many
// grid labels they contain, and Stream is the only one that finds anything where the grid has no
// ruled borders at all (wr51__114222355, each field on its own paragraph line).
// SimpleNurminenDetectionAlgorithm plus BasicExtractionAlgorithm is tabula-sharp's own documented
// Stream-mode pairing.
//
// Cached like the cloud implementations, though only CPU time is saved here, not API cost.
public class TabulaTableExtractorService(ICacheService cacheService) : ITableExtractorService
{
    public string Name => "TabulaSharp";

    /// Cached payloads are keyed by this, not by Name, so a change to what a cell carries can
    /// invalidate the cache without renaming the service in every match's stored provenance. v1
    /// entries hold only Left and Top, which yields no coordinates to highlight, and the cache key
    /// has no component for the payload's shape - so they would be reused indefinitely.
    private const string CacheServiceName = "TabulaSharp-v2";

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
            OcrServiceName = CacheServiceName,
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
        
        var internalDocument = await pdfDocument.OpenInternalDocumentAsync();

        if (internalDocument?.UnderlyingDocument is not UglyToad.PdfPig.PdfDocument document)
        {
            throw new Exception("Tabula can only be used when provider is PdfPig");
        }
        
        var tables = new List<DocumentTable>();
        var latticeAlgorithm = new SpreadsheetExtractionAlgorithm();
        var streamDetector = new SimpleNurminenDetectionAlgorithm();
        var streamAlgorithm = new BasicExtractionAlgorithm();

        for (var pageNumber = 1; pageNumber <= document.NumberOfPages; pageNumber++)
        {
            var page = ObjectExtractor.Extract(document, pageNumber);

            // tabula-sharp drops the second of two identical adjacent characters ("Officer" ->
            // "Of icer"). Its geometry is sound, so rectangles are kept and text comes from PdfPig.
            var pdfPigWords = document.GetPage(pageNumber).GetWords().ToList();

            // A crash in one algorithm must not lose the other's good result for this page.
            try
            {
                tables.AddRange(latticeAlgorithm
                    .Extract(page)
                    .Select(table => ToOcrTable(table, pageNumber, pdfPigWords)));
            }
            catch (Exception ex)
            {
                // Falls through to Stream mode below for this page.
                
                Console.WriteLine($"INFO - {nameof(TabulaTableExtractorService)} - Lattice, exception {ex.Message}, continuing");
            }

            try
            {
                foreach (var region in streamDetector.Detect(page))
                {
                    tables.AddRange(streamAlgorithm
                        .Extract(page.GetArea(region.BoundingBox))
                        .Select(table => ToOcrTable(table, pageNumber, pdfPigWords)));
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

    private static DocumentTable ToOcrTable(
        Table table,
        int pageNumber,
        IReadOnlyList<Word> pdfPigWords)
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
                    Content = GetCellTextFromWords(cell, pdfPigWords) ?? GetCellText(cell).Trim(),
                    Left = cell.Left,
                    Top = cell.Top,
                    Right = cell.Right,
                    Bottom = cell.Bottom
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
    
    /// A word belongs to the cell containing its centre, which stays unambiguous where a bounding
    /// box overlaps a border. Null when no word lands in it, so the caller keeps Tabula's own text.
    private static string? GetCellTextFromWords(Cell cell, IReadOnlyList<Word> pdfPigWords)
    {
        var contained = pdfPigWords
            .Where(word => !string.IsNullOrWhiteSpace(word.Text))
            .Where(word =>
            {
                var box = word.BoundingBox;
                var centreX = (box.Left + box.Right) / 2;
                var centreY = (box.Bottom + box.Top) / 2;

                return centreX >= cell.Left && centreX < cell.Right
                    && centreY >= cell.Bottom && centreY < cell.Top;
            })
            .ToList();

        if (contained.Count == 0)
        {
            return null;
        }

        // Grouped into rows on the word's own height, so a wrapped cell keeps its line breaks.
        var lines = new List<List<Word>>();

        foreach (var word in contained.OrderByDescending(w => w.BoundingBox.Top))
        {
            var line = lines.Count > 0 ? lines[^1] : null;
            var tolerance = Math.Max(word.BoundingBox.Height, 1) / 2;

            if (line != null && Math.Abs(line[0].BoundingBox.Top - word.BoundingBox.Top) <= tolerance)
            {
                line.Add(word);
                continue;
            }

            lines.Add([word]);
        }

        return string.Join(
                "\n",
                lines.Select(line => string.Join(
                    " ",
                    line.OrderBy(w => w.BoundingBox.Left).Select(w => w.Text))))
            .Trim();
    }

    private static string GetCellText(Cell cell)
    {
        var lines = GroupIntoLines(cell.TextElements);
        var outputTextSb = new StringBuilder();
        
        foreach (var line in lines)
        {
            // GroupIntoLines orders only by Top, so two chunks on a row are not necessarily
            // left-to-right and may overlap in X. Flatten first, then sort every letter by Left.
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