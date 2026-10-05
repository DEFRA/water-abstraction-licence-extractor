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
/// Reads the LicenceProvisions/MeasurementDetails grid's drawn table borders straight off a
/// native WR51 PDF's content stream - structurally more precise than the whitespace-gap
/// column-walk heuristic (FindLabelGroupMatchesHelper) the rest of the pipeline uses. WR51 PDFs
/// draw borders as thin filled rectangles (`re` ... `f*`), which pdftotext -layout drops with all
/// other graphics, so they were never used before. GridCellReconstructor turns them into cells
/// (including colspan/rowspan merges), WordToCellAssigner places each PdfPig word by which cell
/// contains its centre, and TableExtractorHelper does the label-to-cell matching from there.
///
/// A 796-document sweep found 3 PdfClown-level failure classes this treats as "found nothing"
/// rather than failing extraction: encrypted PDFs (unsupported), a NullReferenceException in
/// PdfClown's ShowText.Scan, and ~7% of documents with no drawn border grid at all.
///
/// Needs System.Drawing.Common (pinned to 6.0.0, see the csproj) and libgdiplus at runtime for
/// ContentScanner to resolve page-space coordinates - bundle it into the Lambda image alongside
/// the existing Tesseract/Leptonica natives before enabling in production.
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

        // Reads pdfDocument.Bytes directly, not via OpenInternalDocumentAsync(): that fetches
        // bytes by filename through machinery not populated on the ad-hoc PdfDocument
        // GetTableMatchesAsync builds for this overlay, where it throws a NullReferenceException
        // and silently zeroes out every table overlay. Tabula hits the same failure there.
        var bytes = pdfDocument.Bytes;

        if (bytes == null)
        {
            return [];
        }

        using var pdfPigDocument = UglyToad.PdfPig.PdfDocument.Open(bytes);

        Dictionary<int, List<Segment>> segmentsByPage;

        try
        {
            segmentsByPage = ExtractSegmentsByPage(bytes);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"INFO - {nameof(PdfClownGridTableExtractorService)} - " +
                               $"{ex.GetType().Name}: {ex.Message} - returning no tables");
            return [];
        }

        // One DocumentTable per page with a reconstructed grid, never across pages - each page's
        // coordinate space starts fresh, so matching (X, Y) on two pages is coincidence. WR51
        // documents run 2-9 pages (mode 4), so the original page-1-only scope missed real content,
        // multi-meter detail and later General Comments especially.
        var tables = new List<DocumentTable>();

        foreach (var (pageNumber, segments) in segmentsByPage)
        {
            var cells = ReconstructCells(segments.Select(s => new BorderSegment(s.X, s.Y, s.Width, s.Height))).ToList();

            if (cells.Count == 0)
            {
                continue;
            }

            var words = pdfPigDocument.GetPage(pageNumber).GetWords()
                .Select(w => new WordBox(
                    w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Top, w.BoundingBox.Bottom))
                .ToList();

            var populated = AssignWordsToCells(cells, words)
                .Where(kv => kv.Value.Count > 0)
                .ToList();

            if (populated.Count == 0)
            {
                continue;
            }

            tables.Add(BuildDocumentTable(populated, pageNumber));
        }

        // One cache entry under the shared page-1 key however many pages contributed, matching
        // TabulaTableExtractorService's convention.
        await cacheService.SaveOcrImageTextAsync(
            request,
            JsonSerializer.Serialize(tables, JsonHelper.GetSerializerOptions()));

        return tables;
    }

    // These indices only need to order cells within their own row/column, not describe one
    // uniform grid - row-bands on a page genuinely differ in column count. Clustering each axis's
    // distinct positions gives a stable ordinal for TableExtractorHelper's +1 adjacency check.
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

    // Same clustering idea as GridCellReconstructor's line-snapping, but simpler: whole cell
    // positions are already points apart, unlike raw hairline segments.
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

    // org.pdfclown.Version.Get caches into an unlocked static Dictionary, so opening two Files
    // concurrently can corrupt it and throw. Confirmed by running the POC suite in parallel.
    private static readonly Lock PdfClownFileOpenLock = new();

    // Keyed 1-based to match PdfPig's own page numbering (pdfPigDocument.GetPage(n)), since
    // callers pair a page's PdfClown-derived cells with that same page's PdfPig-derived words.
    private static Dictionary<int, List<Segment>> ExtractSegmentsByPage(byte[] bytes)
    {
        org.pdfclown.files.File pdf;

        lock (PdfClownFileOpenLock)
        {
            pdf = new org.pdfclown.files.File(new org.pdfclown.bytes.Buffer(bytes));
        }

        using (pdf)
        {
            var segmentsByPage = new Dictionary<int, List<Segment>>();
            var pageNumber = 0;

            foreach (var page in pdf.Document.Pages)
            {
                pageNumber++;

                var segments = new List<Segment>();
                Scan(new ContentScanner(page), segments);

                if (segments.Count > 0)
                {
                    segmentsByPage[pageNumber] = segments;
                }
            }

            return segmentsByPage;
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

    // A Path's child level holds its construction operators then its terminal paint operator.
    // Only a Filled PaintPath was actually drawn - a ModifyClipPath/no-op terminator (Word's
    // "W* n" per-run text clip box) painted nothing and isn't a border.
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
