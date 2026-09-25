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
/// POC: can PdfClown recover the real drawn table grid (row/column border coordinates) from a
/// WR51 PDF's content stream, independent of PdfPig/pdftotext's text-only view? Confirmed via
/// manual content-stream inspection this session that these documents draw table borders as
/// thin filled rectangles (`re` ... `f*`), not strokes - visible when rendered, invisible to
/// pdftotext -layout (which drops all graphics), and previously never looked at.
/// </summary>
public class PdfClownGridExtractionPocTests(ITestOutputHelper output)
{
    private record Segment(double X, double Y, double Width, double Height)
    {
        public bool IsHorizontal => Width >= Height;
    }

    [Theory]
    [InlineData("wr51__2671321040__ce27146f-c195-4eb3-9048-1f88dd1248ab.pdf", "T1")]
    [InlineData("wr51__940030021sr__373ad293-542f-43aa-baa4-6fc65a7e1b7a.pdf", "T4")]
    [InlineData("wr51__121013s32__0a2edeb3-3803-4d60-810c-e044f6584c30.pdf", "T6")]
    public void PdfClown_ReconstructsGridRowsAndColumns_AcrossTemplates(string filename, string template)
    {
        var segments = ExtractSegments(filename);

        var rows = ClusterPositions(segments.Where(s => s.IsHorizontal).Select(s => s.Y), tolerance: 1.0);
        var columns = ClusterPositions(segments.Where(s => !s.IsHorizontal).Select(s => s.X), tolerance: 1.0);

        output.WriteLine($"{template} {filename}: {segments.Count} segments -> " +
                          $"{rows.Count} distinct row lines, {columns.Count} distinct column lines");

        Assert.True(rows.Count >= 4, $"[{template}] expected several distinct row boundaries, got {rows.Count}");
        Assert.True(columns.Count >= 2, $"[{template}] expected several distinct column boundaries, got {columns.Count}");
    }

    [Fact]
    public void EndToEnd_MatchesTheKnownGridPattern_ForTheLicenceProvisionsAndMeasurementDetailsGrid()
    {
        const string filename = "wr51__2671321040__ce27146f-c195-4eb3-9048-1f88dd1248ab.pdf";

        var (cells, populated) = ExtractGridBandCellsAndWords(filename, yMin: 375, yMax: 630);

        output.WriteLine($"{populated.Count} of {cells.Count} cells populated:");
        foreach (var (cell, cellWords) in populated.OrderByDescending(p => p.Key.Y).ThenBy(p => p.Key.X))
        {
            output.WriteLine($"  [X={cell.X,6:F1} Y={cell.Y,6:F1} W={cell.Width,6:F1} H={cell.Height,6:F1}] " +
                              $"\"{CellText(cellWords)}\"");
        }

        AssertMatchesPattern(populated, ExpectedGridPatterns.T1_2671321040_LicenceProvisionsAndMeasurementDetails);
    }

    [Fact]
    public void EndToEnd_MatchesTheKnownGridPattern_ForTheHeaderBlock()
    {
        const string filename = "wr51__2671321040__ce27146f-c195-4eb3-9048-1f88dd1248ab.pdf";

        var (cells, populated) = ExtractGridBandCellsAndWords(filename, yMin: 610, yMax: 815);

        output.WriteLine($"{populated.Count} of {cells.Count} cells populated:");
        foreach (var (cell, cellWords) in populated.OrderByDescending(p => p.Key.Y).ThenBy(p => p.Key.X))
        {
            output.WriteLine($"  [X={cell.X,6:F1} Y={cell.Y,6:F1} W={cell.Width,6:F1} H={cell.Height,6:F1}] " +
                              $"\"{CellText(cellWords)}\"");
        }

        AssertMatchesPattern(populated, ExpectedGridPatterns.T1_2671321040_HeaderBlock);
    }

    [Fact]
    public void EndToEnd_MatchesTheKnownGridPattern_ForT4()
    {
        const string filename = "wr51__940030021sr__373ad293-542f-43aa-baa4-6fc65a7e1b7a.pdf";

        var (cells, populated) = ExtractGridBandCellsAndWords(filename, yMin: 0, yMax: 900);

        output.WriteLine($"{populated.Count} of {cells.Count} cells populated:");
        foreach (var (cell, cellWords) in populated.OrderByDescending(p => p.Key.Y).ThenBy(p => p.Key.X))
        {
            output.WriteLine($"  [X={cell.X,6:F1} Y={cell.Y,6:F1} W={cell.Width,6:F1} H={cell.Height,6:F1}] " +
                              $"\"{CellText(cellWords)}\"");
        }

        AssertMatchesPattern(populated, ExpectedGridPatterns.T4_940030021sr);
    }

    [Fact]
    public void EndToEnd_MatchesTheKnownGridPattern_ForT6()
    {
        const string filename = "wr51__121013s32__0a2edeb3-3803-4d60-810c-e044f6584c30.pdf";

        var (cells, populated) = ExtractGridBandCellsAndWords(filename, yMin: 0, yMax: 900);

        output.WriteLine($"{populated.Count} of {cells.Count} cells populated:");
        foreach (var (cell, cellWords) in populated.OrderByDescending(p => p.Key.Y).ThenBy(p => p.Key.X))
        {
            output.WriteLine($"  [X={cell.X,6:F1} Y={cell.Y,6:F1} W={cell.Width,6:F1} H={cell.Height,6:F1}] " +
                              $"\"{CellText(cellWords)}\"");
        }

        AssertMatchesPattern(populated, ExpectedGridPatterns.T6_121013s32);
    }

    // Checks that every group's full joined phrase lands in exactly one cell, and that cell
    // contains no other group's phrase - i.e. the reconstruction neither splits a real merged
    // cell apart nor merges two real, separate cells together.
    private static void AssertMatchesPattern(
        List<KeyValuePair<Cell, List<WordBox>>> populated, string[][] pattern)
    {
        var anchors = pattern.Select(g => string.Join(' ', g)).ToList();

        for (var i = 0; i < pattern.Length; i++)
        {
            var anchor = anchors[i];

            var matches = populated
                .Where(p => CellText(p.Value).Contains(anchor, StringComparison.OrdinalIgnoreCase))
                .ToList();

            Assert.True(matches.Count == 1,
                $"expected exactly one cell containing '{anchor}', found {matches.Count}");

            var cellText = CellText(matches[0].Value);

            for (var j = 0; j < pattern.Length; j++)
            {
                if (j != i)
                {
                    Assert.DoesNotContain(anchors[j], cellText, StringComparison.OrdinalIgnoreCase);
                }
            }
        }
    }

    private (List<Cell> Cells, List<KeyValuePair<Cell, List<WordBox>>> Populated) ExtractGridBandCellsAndWords(
        string filename, double yMin, double yMax)
    {
        var path = System.IO.Path.Combine(TestConfig.PdfFolder, filename);

        var segments = ExtractSegments(filename);
        var gridSegments = segments.Where(s => s.Y + s.Height >= yMin && s.Y <= yMax).ToList();
        var cells = ReconstructCells(gridSegments.Select(s => new BorderSegment(s.X, s.Y, s.Width, s.Height)))
            .ToList();

        using var pdfPigDoc = PdfDocument.Open(path);
        var words = pdfPigDoc.GetPage(1).GetWords()
            .Select(w => new WordBox(
                w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Top, w.BoundingBox.Bottom))
            .Where(w => w.Bottom >= yMin && w.Bottom <= yMax)
            .ToList();

        var populated = AssignWordsToCells(cells, words)
            .Where(kv => kv.Value.Count > 0)
            .ToList();

        return (cells, populated);
    }

    // org.pdfclown.Version.Get caches into a plain, unlocked static Dictionary - opening two
    // Files concurrently (this project runs tests in parallel by default) can corrupt it and
    // throw. Confirmed: serializing File construction alone (nothing else in PdfClown showed
    // the same symptom) is enough to make this reliable.
    private static readonly Lock PdfClownFileOpenLock = new();

    private List<Segment> ExtractSegments(string filename)
    {
        var path = System.IO.Path.Combine(TestConfig.PdfFolder, filename);

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

    // Hairline rectangles for the same visual line rarely land on the exact same coordinate
    // (sub-point rendering differences per segment) - group positions within `tolerance` of
    // each other into one logical grid line before counting/reporting them.
    private static List<double> ClusterPositions(IEnumerable<double> positions, double tolerance)
    {
        var sorted = positions.OrderBy(p => p).ToList();
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
