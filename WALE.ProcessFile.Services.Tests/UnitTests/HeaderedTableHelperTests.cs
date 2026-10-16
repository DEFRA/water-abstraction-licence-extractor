using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// The headered-grid reader, covering the copy-selection rule and the cell regions it now returns.
/// Its text is deliberately not the cell's own content - it normalises whitespace and blanks a
/// placeholder - which is why a value carries its region alongside the text rather than callers
/// being handed the cell.
/// </summary>
public class HeaderedTableHelperTests
{
    private static readonly string[] Headers = ["Reference", "Type", "Action", "Complete by"];

    private const string Placeholder = "Choose an item.";

    private static DocumentTableCell Cell(int row, int col, string content, double? left = null) => new()
    {
        RowIndex = row,
        ColumnIndex = col,
        Content = content,
        Left = left,
        Top = left == null ? null : 10,
        Right = left == null ? null : left + 50,
        Bottom = left == null ? null : 20
    };

    private static IEnumerable<DocumentTableCell> HeaderRow(int row) =>
        Headers.Select((header, index) => Cell(row, index, header));

    [Fact]
    public void WhenRowsAreRead_ThenEachValueCarriesItsOwnCellRegion()
    {
        var table = new DocumentTable
        {
            ColumnCount = 4,
            RowCount = 2,
            Cells =
            [
                ..HeaderRow(0),
                Cell(1, 0, "A1", left: 10),
                Cell(1, 1, "Advice", left: 60),
                Cell(1, 2, "Fit a meter", left: 110),
                Cell(1, 3, "01/04/2026", left: 160)
            ]
        };

        var row = Assert.Single(HeaderedTableHelper.ReadRows([table], Headers));

        Assert.Equal("Fit a meter", row["Action"].Text);
        Assert.Equal(110, row["Action"].Coordinates!.Left);
        Assert.Equal(160, row["Complete by"].Coordinates!.Left);
    }

    [Fact]
    public void WhenAValueIsAPlaceholder_ThenTextIsBlankedButTheRegionIsKept()
    {
        // The blank holds the row's position for the converter's index-zip, and the cell is still
        // a real place on the page, so a verifier can be shown where the unfilled dropdown sits.
        var table = new DocumentTable
        {
            ColumnCount = 4,
            RowCount = 2,
            Cells =
            [
                ..HeaderRow(0),
                Cell(1, 0, Placeholder, left: 10),
                Cell(1, 1, "Advice", left: 60),
                Cell(1, 2, "Fit a meter", left: 110),
                Cell(1, 3, string.Empty, left: 160)
            ]
        };

        var row = Assert.Single(HeaderedTableHelper.ReadRows([table], Headers, [Placeholder]));

        Assert.Equal(string.Empty, row["Reference"].Text);
        Assert.Equal(10, row["Reference"].Coordinates!.Left);
    }

    [Fact]
    public void WhenEveryValueInARowIsBlankOrPlaceholder_ThenTheRowIsNotARecord()
    {
        var table = new DocumentTable
        {
            ColumnCount = 4,
            RowCount = 2,
            Cells =
            [
                ..HeaderRow(0),
                Cell(1, 0, Placeholder, left: 10),
                Cell(1, 1, Placeholder, left: 60),
                Cell(1, 2, "   ", left: 110),
                Cell(1, 3, string.Empty, left: 160)
            ]
        };

        Assert.Empty(HeaderedTableHelper.ReadRows([table], Headers, [Placeholder]));
    }

    [Fact]
    public void WhenTheGridIsEmittedTwice_ThenTheCopyCarryingTheFullHeaderTextIsReadWithItsRegions()
    {
        // The fragmented copy splits a wrapping column name and keeps only its first word, so
        // requiring the full header text is what separates the usable copy from the other.
        var fragmented = new DocumentTable
        {
            ColumnCount = 4,
            RowCount = 3,
            Cells =
            [
                Cell(0, 0, "Reference"), Cell(0, 1, "Type"), Cell(0, 2, "Action"),
                Cell(0, 3, "Complete"),
                Cell(1, 2, "Fit a", left: 900),
                Cell(2, 2, "meter", left: 900)
            ]
        };

        var clean = new DocumentTable
        {
            ColumnCount = 4,
            RowCount = 2,
            Cells =
            [
                ..HeaderRow(0),
                Cell(1, 0, "A1", left: 10),
                Cell(1, 1, "Advice", left: 60),
                Cell(1, 2, "Fit a meter", left: 110),
                Cell(1, 3, "01/04/2026", left: 160)
            ]
        };

        var row = Assert.Single(HeaderedTableHelper.ReadRows([fragmented, clean], Headers));

        Assert.Equal("Fit a meter", row["Action"].Text);
        Assert.Equal(110, row["Action"].Coordinates!.Left);
    }

    [Fact]
    public void WhenNoTableCarriesTheHeaders_ThenNothingIsRead()
    {
        var table = new DocumentTable
        {
            ColumnCount = 2,
            RowCount = 1,
            Cells = [Cell(0, 0, "Meter make and model"), Cell(0, 1, "ABB", left: 10)]
        };

        Assert.Empty(HeaderedTableHelper.ReadRows([table], Headers));
    }
}
