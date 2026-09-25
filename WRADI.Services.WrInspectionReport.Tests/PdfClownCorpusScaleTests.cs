using System.Collections.Concurrent;
using org.pdfclown.documents.contents;
using org.pdfclown.documents.contents.objects;
using UglyToad.PdfPig;
using WRADI.Services.WrInspectionReport.Tests.Config;
using Xunit.Abstractions;
using static WALE.ProcessFile.Services.PdfClown.GridCellReconstructor;
using static WALE.ProcessFile.Services.PdfClown.WordToCellAssigner;
using PdfClownPath = org.pdfclown.documents.contents.objects.Path;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Does the PdfClown grid-extraction approach (see PdfClownGridExtractionPocTests,
/// GridCellReconstructor, WordToCellAssigner) hold up across the real 789-document WR51
/// corpus, not just the 3 hand-picked documents it was built and debugged against? No
/// hand-labelled ground truth exists for the whole corpus (unlike the golden set the existing
/// harness uses), so this measures structural signals instead: does it run without exceptions,
/// does every document draw a real border grid at all, and what fraction of each page's text
/// lands inside some reconstructed cell versus falling outside every one.
/// </summary>
public class PdfClownCorpusScaleTests(ITestOutputHelper output)
{
    private record Segment(double X, double Y, double Width, double Height)
    {
        public bool IsHorizontal => Width >= Height;
    }

    private record DocResult(
        string FileName,
        int SegmentCount,
        int CellCount,
        int TotalWords,
        int AssignedWords,
        string? Error);

    [Fact]
    public async Task WhenRunAcrossTheRealWr51Corpus_ThenReportsStructuralCoverage()
    {
        var pdfFolder = TestConfig.PdfFolder;

        var files = Directory.GetFiles(pdfFolder, "*.pdf")
            .Select(System.IO.Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .Where(f => f.StartsWith("wr51", StringComparison.OrdinalIgnoreCase)
                && !f.Contains("dummy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(files.Count > 0, $"No WR51 PDFs found in {pdfFolder}");
        output.WriteLine($"{files.Count} real WR51 documents found");

        var results = new ConcurrentBag<DocResult>();

        // PdfClown's own File construction isn't thread-safe (see the lock in
        // PdfClownGridExtractionPocTests) - segment extraction runs serially; word extraction
        // and assignment (pure PdfPig + our own geometry code) still parallelise fine.
        foreach (var file in files)
        {
            var path = System.IO.Path.Combine(pdfFolder, file);

            try
            {
                var segments = ExtractSegments(path);
                var cells = ReconstructCells(segments.Select(s => new BorderSegment(s.X, s.Y, s.Width, s.Height)))
                    .ToList();

                using var pdfPigDoc = PdfDocument.Open(path);
                var words = pdfPigDoc.GetPage(1).GetWords()
                    .Select(w => new WordBox(
                        w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Top, w.BoundingBox.Bottom))
                    .ToList();

                var assignedCount = AssignWordsToCells(cells, words).Sum(kv => kv.Value.Count);

                results.Add(new DocResult(file, segments.Count, cells.Count, words.Count, assignedCount, null));
            }
            catch (Exception ex)
            {
                results.Add(new DocResult(file, 0, 0, 0, 0, ex.GetType().Name + ": " + ex.Message));
            }
        }

        var resultList = results.ToList();
        var errored = resultList.Where(r => r.Error != null).ToList();
        var noSegments = resultList.Where(r => r.Error == null && r.SegmentCount == 0).ToList();
        var withGrid = resultList.Where(r => r.Error == null && r.SegmentCount > 0).ToList();

        output.WriteLine($"Total: {resultList.Count}");
        output.WriteLine($"Exceptions: {errored.Count}");
        output.WriteLine($"Zero border segments (no vector grid at all): {noSegments.Count}");
        output.WriteLine($"Has a real border grid: {withGrid.Count}");

        if (errored.Count > 0)
        {
            output.WriteLine("Sample exceptions:");
            foreach (var e in errored.Take(10))
            {
                output.WriteLine($"  {e.FileName}: {e.Error}");
            }
        }

        if (withGrid.Count > 0)
        {
            var cellCounts = withGrid.Select(r => r.CellCount).OrderBy(c => c).ToList();
            var coverageRatios = withGrid
                .Where(r => r.TotalWords > 0)
                .Select(r => (double)r.AssignedWords / r.TotalWords)
                .OrderBy(c => c)
                .ToList();

            output.WriteLine($"Cell count - min: {cellCounts[0]}, median: {cellCounts[cellCounts.Count / 2]}, " +
                              $"max: {cellCounts[^1]}");
            output.WriteLine($"Word coverage ratio (words landing in some cell / total page-1 words) - " +
                              $"min: {coverageRatios[0]:P0}, " +
                              $"p10: {coverageRatios[coverageRatios.Count / 10]:P0}, " +
                              $"median: {coverageRatios[coverageRatios.Count / 2]:P0}, " +
                              $"p90: {coverageRatios[coverageRatios.Count * 9 / 10]:P0}, " +
                              $"max: {coverageRatios[^1]:P0}");

            var lowCoverage = withGrid
                .Where(r => r.TotalWords > 0 && (double)r.AssignedWords / r.TotalWords < 0.5)
                .OrderBy(r => (double)r.AssignedWords / r.TotalWords)
                .ToList();

            output.WriteLine($"Documents with <50% word coverage: {lowCoverage.Count}");
            foreach (var r in lowCoverage.Take(15))
            {
                output.WriteLine($"  {r.FileName}: {r.AssignedWords}/{r.TotalWords} " +
                                  $"({(double)r.AssignedWords / r.TotalWords:P0}), {r.CellCount} cells, " +
                                  $"{r.SegmentCount} segments");
            }
        }

        // Known, PdfClown-level limitations, not bugs in this project's own code - confirmed
        // by stack trace: 2 "Encrypted files are currently not supported" (needs the
        // decrypt-with-iTextSharp-first workaround the old task/wradi-208-pdfclown branch
        // already used) and 3 NullReferenceException inside PdfClown's own
        // ShowText.Scan (its font/text handling, unrelated to border-rectangle extraction).
        // A regression here - either a new exception shape or the known 5 growing - is worth
        // failing on; these specific 5 documents aren't.
        Assert.True(errored.Count <= 5,
            $"Expected at most the 5 known PdfClown-level failures, got {errored.Count}");

        Assert.True(withGrid.Count >= resultList.Count * 0.85,
            $"Expected a real border grid on at least 85% of documents, got " +
            $"{withGrid.Count}/{resultList.Count} ({(double)withGrid.Count / resultList.Count:P0})");

        var medianCoverage = withGrid
            .Where(r => r.TotalWords > 0)
            .Select(r => (double)r.AssignedWords / r.TotalWords)
            .OrderBy(c => c)
            .ToList() is { Count: > 0 } ratios
            ? ratios[ratios.Count / 2]
            : 0;

        Assert.True(medianCoverage >= 0.9,
            $"Expected median word coverage of at least 90% across documents with a real grid, got {medianCoverage:P0}");
    }

    // org.pdfclown.Version.Get caches into a plain, unlocked static Dictionary - opening two
    // Files concurrently can corrupt it and throw (see the same lock in
    // PdfClownGridExtractionPocTests). Kept as a distinct copy here rather than shared, since
    // this corpus test is deliberately not using the same test class/fixture.
    private static readonly Lock PdfClownFileOpenLock = new();

    private static List<Segment> ExtractSegments(string path)
    {
        org.pdfclown.files.File pdf;

        lock (PdfClownFileOpenLock)
        {
            pdf = new org.pdfclown.files.File(path);
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
