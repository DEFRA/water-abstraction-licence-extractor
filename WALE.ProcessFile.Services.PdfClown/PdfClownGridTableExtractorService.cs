using System.Text.Json;
using org.pdfclown.documents.contents;
using org.pdfclown.documents.contents.objects;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OcrService;
using static WALE.ProcessFile.Services.PdfClown.GridCellReconstructor;
using static WALE.ProcessFile.Services.PdfClown.WordToCellAssigner;
using PdfClownPath = org.pdfclown.documents.contents.objects.Path;
using PdfDocument = WALE.ProcessFile.Core.Models.PdfDocument;

namespace WALE.ProcessFile.Services.PdfClown;

/// <summary>
/// Reads the LicenceProvisions/MeasurementDetails grid's real drawn table borders directly off
/// a native WR51 PDF's content stream - independent of, and structurally more precise than, the
/// whitespace-gap column-walk heuristic (FindLabelGroupMatchesHelper) the rest of the pipeline
/// relies on. See WALE.ProcessFile.Services.PdfClown.Tests for the full derivation: WR51 PDFs
/// draw table borders as thin filled rectangles (`re` ... `f*`), invisible to pdftotext -layout
/// (drops all graphics) and so never used before this. GridCellReconstructor turns those
/// rectangles into real cells (including colspan/rowspan merges); WordToCellAssigner places
/// each PdfPig word by which cell's bounds contain its center. TableExtractorHelper (already
/// used by TabulaTableExtractorService/AzureAiServicesDocumentIntelligenceTableExtractorService)
/// does the label-to-cell matching from there, reusing the label configuration's own possibility
/// lists unchanged.
///
/// A 796-document real-corpus structural sweep found 3 narrow, PdfClown-level failure classes
/// this extractor treats as "found nothing" rather than crashing the whole extraction:
/// encrypted PDFs (PdfClown doesn't support them at all), a NullReferenceException inside
/// PdfClown's own ShowText.Scan (unrelated to the border-rectangle logic here), and ~7% of
/// documents with no drawn border grid at all (a different PDF generator, presumably).
///
/// Needs System.Drawing.Common (pinned to 6.0.0 - see this project's own csproj comment) and
/// libgdiplus at runtime for PdfClown's ContentScanner to resolve each rectangle's real
/// page-space coordinates - bundle it into the Lambda deployment alongside this pipeline's
/// existing Tesseract/Leptonica native dependencies before enabling this in production.
/// </summary>
public class PdfClownGridTableExtractorService(ICacheService cacheService) : ITableExtractorService
{
    public string Name => "PdfClown";
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
            return JsonSerializer.Deserialize<List<DocumentTable>>(cacheText, JsonHelper.GetSerializerOptions())!;
        }

        // Deliberately reads pdfDocument.Bytes directly rather than going through
        // OpenInternalDocumentAsync() - that path calls out to NoOcrPdfDocumentService/
        // IFileService to fetch bytes by filename, machinery this extractor doesn't need since
        // the bytes are already in hand, and which isn't populated on the ad-hoc PdfDocument
        // WrInspectionReportExtractionOrchestrator.GetTableMatchesAsync constructs for this
        // overlay (confirmed: calling OpenInternalDocumentAsync() there throws a
        // NullReferenceException, silently zeroing out every table-extractor overlay through
        // that path - not specific to this service, TabulaTableExtractorService hits the same
        // failure there).
        var bytes = pdfDocument.Bytes;

        if (bytes == null)
        {
            return [];
        }

        using var pdfPigDocument = UglyToad.PdfPig.PdfDocument.Open(bytes);

        List<Segment> segments;

        try
        {
            segments = ExtractSegments(bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"INFO - {nameof(PdfClownGridTableExtractorService)} - " +
                               $"{ex.GetType().Name}: {ex.Message} - returning no tables");
            return [];
        }

        var cells = ReconstructCells(segments.Select(s => new BorderSegment(s.X, s.Y, s.Width, s.Height))).ToList();

        if (cells.Count == 0)
        {
            return [];
        }

        var words = pdfPigDocument.GetPage(SharedPageNumber).GetWords()
            .Select(w => new WordBox(
                w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Top, w.BoundingBox.Bottom))
            .ToList();

        var populated = AssignWordsToCells(cells, words)
            .Where(kv => kv.Value.Count > 0)
            .ToList();

        var tables = new List<DocumentTable> { BuildDocumentTable(populated, SharedPageNumber) };

        await cacheService.SaveOcrImageTextAsync(
            request,
            JsonSerializer.Serialize(tables, JsonHelper.GetSerializerOptions()));

        return tables;
    }

    // Row/column index only needs to correctly order cells within their own row/column - it
    // doesn't need to describe one globally uniform grid shape (different row-bands on the same
    // page genuinely have different column counts). Clustering each axis's distinct cell
    // positions gives every cell a stable ordinal that TableExtractorHelper's
    // RowIndex/ColumnIndex+1 adjacency check (for a value sitting in a cell separate from its
    // label - a checkbox tick, say) can use correctly.
    private static DocumentTable BuildDocumentTable(
        List<KeyValuePair<Cell, List<WordBox>>> populated, int pageNumber)
    {
        var rows = ClusterPositions(populated.Select(p => p.Key.Y), tolerance: 2.0)
            .OrderByDescending(y => y) // PdfPig/PdfClown Y-up - row 0 is the top of the page
            .ToList();
        var columns = ClusterPositions(populated.Select(p => p.Key.X), tolerance: 2.0)
            .OrderBy(x => x)
            .ToList();

        var cells = populated.Select(p => new DocumentTableCell
        {
            RowIndex = ClosestIndex(rows, p.Key.Y),
            ColumnIndex = ClosestIndex(columns, p.Key.X),
            Content = CellText(p.Value),
            Left = p.Key.X,
            Top = p.Key.Y
        }).ToList();

        return new DocumentTable
        {
            PageNumber = pageNumber,
            RowCount = rows.Count,
            ColumnCount = columns.Count,
            Cells = cells,
            TableType = DocumentTableType.Structured
        };
    }

    private static int ClosestIndex(IReadOnlyList<double> positions, double value)
    {
        var closest = 0;
        var closestDistance = double.MaxValue;

        for (var i = 0; i < positions.Count; i++)
        {
            var distance = Math.Abs(positions[i] - value);

            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = i;
            }
        }

        return closest;
    }

    // Same clustering approach as GridCellReconstructor's own line-snapping (a separate,
    // simpler pass here - this is grouping whole CELL positions, already several points apart
    // by construction, not raw hairline segments needing the tighter crossing-gap distinction).
    private static List<double> ClusterPositions(IEnumerable<double> positions, double tolerance)
    {
        var sorted = positions.Distinct().OrderBy(p => p).ToList();
        var clusters = new List<double>();

        foreach (var p in sorted)
        {
            if (clusters.Count == 0 || p - clusters[^1] > tolerance)
            {
                clusters.Add(p);
            }
        }

        return clusters;
    }

    private record Segment(double X, double Y, double Width, double Height)
    {
        public bool IsHorizontal => Width >= Height;
    }

    // org.pdfclown.Version.Get caches into a plain, unlocked static Dictionary - opening two
    // Files concurrently can corrupt it and throw. Confirmed via this project's own POC test
    // suite running documents in parallel this session.
    private static readonly Lock PdfClownFileOpenLock = new();

    private static List<Segment> ExtractSegments(byte[] bytes)
    {
        org.pdfclown.files.File pdf;

        lock (PdfClownFileOpenLock)
        {
            pdf = new org.pdfclown.files.File(new org.pdfclown.bytes.Buffer(bytes));
        }

        using (pdf)
        {
            var page = pdf.Document.Pages[0];

            var segments = new List<Segment>();
            Scan(new ContentScanner(page), segments);
            return segments;
        }
    }

    private static void Scan(ContentScanner? scanner, List<Segment> segments)
    {
        if (scanner == null)
        {
            return;
        }

        while (scanner.MoveNext())
        {
            var current = scanner.Current;

            if (current is PdfClownPath)
            {
                CollectPathRectangles(scanner.ChildLevel, segments);
                continue;
            }

            if (current is CompositeObject)
            {
                Scan(scanner.ChildLevel, segments);
            }
        }
    }

    // A Path's own child level holds its construction operators (DrawRectangle etc.) followed
    // by its terminal paint operator. Only a Filled PaintPath means this rectangle was actually
    // drawn on the page - a ModifyClipPath/no-op terminator (the "W* n" shape Word emits for
    // every per-run text clip box) means it never painted anything and isn't a real border.
    private static void CollectPathRectangles(ContentScanner? pathLevel, List<Segment> segments)
    {
        if (pathLevel == null)
        {
            return;
        }

        var rectangles = new List<DrawRectangle>();
        var filled = false;

        while (pathLevel.MoveNext())
        {
            switch (pathLevel.Current)
            {
                case DrawRectangle rect:
                    rectangles.Add(rect);
                    break;
                case PaintPath { Filled: true }:
                    filled = true;
                    break;
            }
        }

        if (!filled || rectangles.Count == 0)
        {
            return;
        }

        foreach (var rect in rectangles)
        {
            var p1 = pathLevel.State.UserToDeviceSpace(new System.Drawing.PointF((float)rect.X, (float)rect.Y));
            var p2 = pathLevel.State.UserToDeviceSpace(
                new System.Drawing.PointF((float)(rect.X + rect.Width), (float)(rect.Y + rect.Height)));

            var x = Math.Min(p1.X, p2.X);
            var y = Math.Min(p1.Y, p2.Y);
            var w = Math.Abs(p2.X - p1.X);
            var h = Math.Abs(p2.Y - p1.Y);

            segments.Add(new Segment(x, y, w, h));
        }
    }
}
