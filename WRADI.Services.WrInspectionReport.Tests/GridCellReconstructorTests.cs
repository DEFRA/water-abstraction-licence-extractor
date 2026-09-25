using static WALE.ProcessFile.Services.PdfClown.GridCellReconstructor;

namespace WRADI.Services.WrInspectionReport.Tests;

public class GridCellReconstructorTests
{
    private static BorderSegment H(double y, double x1, double x2) => new(x1, y, x2 - x1, 0.7);
    private static BorderSegment V(double x, double y1, double y2) => new(x, y1, 0.7, y2 - y1);

    [Fact]
    public void UniformTwoByTwoGrid_ReconstructsFourEqualCells()
    {
        BorderSegment[] segments =
        [
            H(0, 0, 200), H(50, 0, 200), H(100, 0, 200),
            V(0, 0, 100), V(100, 0, 100), V(200, 0, 100)
        ];

        var cells = ReconstructCells(segments);

        Assert.Equal(4, cells.Count);
        Assert.Contains(cells, c => c is { X: 0, Y: 0, Width: 100, Height: 50 });
        Assert.Contains(cells, c => c is { X: 100, Y: 0, Width: 100, Height: 50 });
        Assert.Contains(cells, c => c is { X: 0, Y: 50, Width: 100, Height: 50 });
        Assert.Contains(cells, c => c is { X: 100, Y: 50, Width: 100, Height: 50 });
    }

    [Fact]
    public void MissingInternalVerticalDivider_MergesTopRowIntoOneCell()
    {
        // Same shape as "Land (only if specified): n/a" - one cell spans the width that would
        // otherwise be its own column plus the neighbour's, because the divider between them
        // simply isn't drawn for that row.
        BorderSegment[] segments =
        [
            H(0, 0, 200), H(50, 0, 200), H(100, 0, 200),
            V(0, 0, 100), V(200, 0, 100),
            V(100, 50, 100) // only present for the bottom row, not the top
        ];

        var cells = ReconstructCells(segments);

        Assert.Equal(3, cells.Count);
        Assert.Contains(cells, c => c is { X: 0, Y: 0, Width: 200, Height: 50 }); // merged top row
        Assert.Contains(cells, c => c is { X: 0, Y: 50, Width: 100, Height: 50 });
        Assert.Contains(cells, c => c is { X: 100, Y: 50, Width: 100, Height: 50 });
    }

    [Fact]
    public void MissingInternalHorizontalDivider_MergesLeftColumnIntoOneCell()
    {
        // Same shape as "Other provisions (specify below): n/a" - one cell spans 2 row-slots
        // because the divider between them isn't drawn for that column.
        BorderSegment[] segments =
        [
            H(0, 0, 200), H(100, 0, 200),
            H(50, 100, 200), // only present for the right column, not the left
            V(0, 0, 100), V(100, 0, 100), V(200, 0, 100)
        ];

        var cells = ReconstructCells(segments);

        Assert.Equal(3, cells.Count);
        Assert.Contains(cells, c => c is { X: 0, Y: 0, Width: 100, Height: 100 }); // merged left column
        Assert.Contains(cells, c => c is { X: 100, Y: 0, Width: 100, Height: 50 });
        Assert.Contains(cells, c => c is { X: 100, Y: 50, Width: 100, Height: 50 });
    }

    [Fact]
    public void ABorderLineDrawnAsTwoAbuttingSegments_IsTreatedAsOneContinuousLine()
    {
        // Word draws a single visual border as several separate rectangles - two touching
        // (not overlapping) segments at the same Y must still count as one continuous line,
        // or every cell touching the join would wrongly fail its edge-coverage check.
        BorderSegment[] segments =
        [
            H(0, 0, 100), H(0, 100, 200), // one line, drawn in two pieces
            H(50, 0, 200),
            V(0, 0, 50), V(100, 0, 50), V(200, 0, 50)
        ];

        var cells = ReconstructCells(segments);

        Assert.Equal(2, cells.Count);
        Assert.Contains(cells, c => c is { X: 0, Y: 0, Width: 100, Height: 50 });
        Assert.Contains(cells, c => c is { X: 100, Y: 0, Width: 100, Height: 50 });
    }

    [Fact]
    public void NoSegments_ReturnsNoCells()
    {
        var cells = ReconstructCells([]);

        Assert.Empty(cells);
    }
}
