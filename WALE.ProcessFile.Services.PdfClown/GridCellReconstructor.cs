namespace WALE.ProcessFile.Services.PdfClown;

/// <summary>
/// Pure geometry: turns the filled hairline rectangles PdfClown recovers from a page's content
/// stream into real table cells, including merges spanning columns or rows, without assuming a
/// uniform grid. Independent of PdfClown/PDF types so it can be tested with synthetic rectangles
/// in milliseconds.
/// </summary>
public static class GridCellReconstructor
{
    public readonly record struct BorderSegment(double X, double Y, double Width, double Height)
    {
        public bool IsHorizontal => Width >= Height;
    }

    public readonly record struct Cell(double X, double Y, double Width, double Height);

    private readonly record struct Interval(double Start, double End);

    public static List<Cell> ReconstructCells(
        IEnumerable<BorderSegment> segments,
        double snapTolerance = 1.0,
        double crossingGapTolerance = 2.0)
    {
        var segmentList = segments.ToList();

        var horizontalLines = BuildLines(
            segmentList.Where(s => s.IsHorizontal).Select(s => (Key: s.Y, Start: s.X, End: s.X + s.Width)),
            snapTolerance);
        var verticalLines = BuildLines(
            segmentList.Where(s => !s.IsHorizontal).Select(s => (Key: s.X, Start: s.Y, End: s.Y + s.Height)),
            snapTolerance);

        var xs = verticalLines.Keys.OrderBy(x => x).ToList();
        var ys = horizontalLines.Keys.OrderBy(y => y).ToList();

        // crossingGapTolerance, not the tighter snapTolerance: a divider's drawn piece is often
        // inset ~1-1.5pt from the true boundary because the corner comes from the other axis's
        // hairline. On a real T1 document a tolerance of 1.0 was too tight for a 1.5pt inset, so a
        // real divider failed its coverage check and never blocked a merge.
        bool HasVerticalCoverage(double x, double y1, double y2) =>
            verticalLines.TryGetValue(x, out var intervals)
            && Covers(intervals, y1, y2, crossingGapTolerance, crossingGapTolerance);

        bool HasHorizontalCoverage(double y, double x1, double x2) =>
            horizontalLines.TryGetValue(y, out var intervals)
            && Covers(intervals, x1, x2, crossingGapTolerance, crossingGapTolerance);

        var cells = new List<Cell>();

        for (var i = 0; i < xs.Count; i++)
        {
            for (var j = i + 1; j < xs.Count; j++)
            {
                var left = xs[i];
                var right = xs[j];

                for (var k = 0; k < ys.Count; k++)
                {
                    for (var l = k + 1; l < ys.Count; l++)
                    {
                        var top = ys[k];
                        var bottom = ys[l];

                        if (!HasVerticalCoverage(left, top, bottom)
                            || !HasVerticalCoverage(right, top, bottom)
                            || !HasHorizontalCoverage(top, left, right)
                            || !HasHorizontalCoverage(bottom, left, right))
                        {
                            continue;
                        }

                        // A real divider anywhere in this span means (i,j)/(k,l) isn't one cell,
                        // just several that happen to share outer borders.
                        var hasInternalRowDivider = false;

                        for (var m = k + 1; m < l; m++)
                        {
                            if (HasHorizontalCoverage(ys[m], left, right))
                            {
                                hasInternalRowDivider = true;
                                break;
                            }
                        }

                        if (hasInternalRowDivider)
                        {
                            continue;
                        }

                        var hasInternalColumnDivider = false;

                        for (var n = i + 1; n < j; n++)
                        {
                            if (HasVerticalCoverage(xs[n], top, bottom))
                            {
                                hasInternalColumnDivider = true;
                                break;
                            }
                        }

                        if (hasInternalColumnDivider)
                        {
                            continue;
                        }

                        cells.Add(new Cell(left, top, right - left, bottom - top));
                    }
                }
            }
        }

        return cells;
    }

    // Groups segments whose Key (Y for horizontal, X for vertical) falls within snapTolerance into
    // one logical line. Deliberately does NOT merge that line's pieces beyond near-zero-gap
    // touching (the 0.15 below): a line crossed by a perpendicular divider is drawn as pieces with
    // a real ~0.5-1.4pt gap at the crossing, and closing it here would erase the divider. Covers()
    // bridges genuine crossing gaps later, where a hairline's gap can't be mistaken for a missing
    // divider's whole-cell one.
    private static Dictionary<double, List<Interval>> BuildLines(
        IEnumerable<(double Key, double Start, double End)> raw,
        double snapTolerance)
    {
        var byKey = new List<(double Key, double Start, double End)>();

        foreach (var item in raw.OrderBy(r => r.Key))
        {
            var existingKey = byKey.Count > 0 && Math.Abs(byKey[^1].Key - item.Key) <= snapTolerance
                ? byKey[^1].Key
                : (double?)null;

            byKey.Add((existingKey ?? item.Key, item.Start, item.End));
        }

        var result = new Dictionary<double, List<Interval>>();

        foreach (var group in byKey.GroupBy(x => x.Key))
        {
            const double touchingTolerance = 0.15;
            var merged = new List<Interval>();

            foreach (var (_, start, end) in group.OrderBy(g => g.Start))
            {
                if (merged.Count > 0 && start <= merged[^1].End + touchingTolerance)
                {
                    merged[^1] = merged[^1] with { End = Math.Max(merged[^1].End, end) };
                }
                else
                {
                    merged.Add(new Interval(start, end));
                }
            }

            result[group.Key] = merged;
        }

        return result;
    }

    // Whether this line's pieces cover [start,end] within boundaryTolerance, bridging gaps up to
    // crossingGapTolerance (a perpendicular divider's thickness, not a real break). Corner and cap
    // rendering means a divider rarely lines up with the boundary to the sub-point.
    private static bool Covers(
        IReadOnlyList<Interval> intervals,
        double start,
        double end,
        double boundaryTolerance,
        double crossingGapTolerance)
    {
        if (intervals.Count == 0)
        {
            return false;
        }

        var sorted = intervals.OrderBy(iv => iv.Start).ToList();
        var runStart = sorted[0].Start;
        var runEnd = sorted[0].End;

        bool RunCovers() => runStart <= start + boundaryTolerance && runEnd >= end - boundaryTolerance;

        foreach (var iv in sorted.Skip(1))
        {
            if (iv.Start <= runEnd + crossingGapTolerance)
            {
                runEnd = Math.Max(runEnd, iv.End);
                continue;
            }

            if (RunCovers())
            {
                return true;
            }

            runStart = iv.Start;
            runEnd = iv.End;
        }

        return RunCovers();
    }
}
