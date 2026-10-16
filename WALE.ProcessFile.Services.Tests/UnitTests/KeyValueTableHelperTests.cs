using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// The two-column key/value reader. Each value carries the region of the cell it came from, and
/// the cases that matter are the ones where the text and the region could disagree:
/// longest-value-wins and the repeating-group merge both choose between copies of the same key, so
/// taking the text from one copy and the region from another reads correctly while outlining the
/// wrong part of the page.
/// </summary>
public class KeyValueTableHelperTests
{
    private static DocumentTableCell Cell(int row, int col, string content, double? left = null) => new()
    {
        RowIndex = row,
        ColumnIndex = col,
        Content = content,
        Left = left,
        Top = left == null ? null : 10,
        Right = left == null ? null : left + 100,
        Bottom = left == null ? null : 20
    };

    private static DocumentTable Table(params DocumentTableCell[] cells) =>
        new() { ColumnCount = 2, RowCount = cells.Length / 2, Cells = cells.ToList() };

    [Fact]
    public void WhenCellReportsARectangle_ThenTheValueCarriesIt()
    {
        var pairs = KeyValueTableHelper.ReadPairs(
            [Table(Cell(0, 0, "Licence number"), Cell(0, 1, "1/2/3", left: 200))]);

        var value = KeyValueTableHelper.FindValue(pairs, "Licence number");

        Assert.Equal("1/2/3", value!.Text);
        Assert.Equal(200, value.Coordinates!.Left);
        Assert.Equal(300, value.Coordinates.Right);
    }

    [Fact]
    public void WhenCellReportsNoRectangle_ThenCoordinatesAreNullRatherThanAZeroRectangle()
    {
        // A fabricated 0,0,0,0 would outline the page corner, which reads as a located value.
        // Null is what DocumentLineColumn.TextToWords turns into UnknownCoordinates.
        var pairs = KeyValueTableHelper.ReadPairs(
            [Table(Cell(0, 0, "Licence number"), Cell(0, 1, "1/2/3"))]);

        Assert.Null(KeyValueTableHelper.FindValue(pairs, "Licence number")!.Coordinates);
    }

    [Fact]
    public void WhenTheSameKeyAppearsTwice_ThenTheRegionComesFromTheCopyWhoseTextWon()
    {
        // The real shape: an extractor reports the same region as both a 7x2 and an 18x2 table,
        // one holding a wrapped value whole where the other has truncated it.
        var pairs = KeyValueTableHelper.ReadPairs(
        [
            Table(Cell(0, 0, "Site address"), Cell(0, 1, "The Farm", left: 50)),
            Table(Cell(0, 0, "Site address"), Cell(0, 1, "The Farm, Long Lane, Kent", left: 400))
        ]);

        var value = KeyValueTableHelper.FindValue(pairs, "Site address");

        Assert.Equal("The Farm, Long Lane, Kent", value!.Text);
        Assert.Equal(400, value.Coordinates!.Left);
    }

    [Fact]
    public void WhenKeyIsMatchedOnItsStem_ThenTheFoundCellsRegionIsReturned()
    {
        var pairs = KeyValueTableHelper.ReadPairs(
            [Table(Cell(0, 0, "Site address (if different)"), Cell(0, 1, "The Farm", left: 75))]);

        var value = KeyValueTableHelper.FindValue(pairs, "Site address");

        Assert.Equal("The Farm", value!.Text);
        Assert.Equal(75, value.Coordinates!.Left);
    }

    [Fact]
    public void WhenAnExactKeyAndALongerOneBothExist_ThenTheExactMatchAndItsRegionWin()
    {
        var pairs = KeyValueTableHelper.ReadPairs(
        [
            Table(
                Cell(0, 0, "Site address"), Cell(0, 1, "Exact", left: 10),
                Cell(1, 0, "Site address (if different)"), Cell(1, 1, "Longer key", left: 500))
        ]);

        var value = KeyValueTableHelper.FindValue(pairs, "Site address");

        Assert.Equal("Exact", value!.Text);
        Assert.Equal(10, value.Coordinates!.Left);
    }

    [Fact]
    public void WhenRepeatingGroupsAreRead_ThenEachGroupsValuesCarryTheirOwnRegions()
    {
        var groups = KeyValueTableHelper.ReadRepeatingGroups(
        [
            Table(
                Cell(0, 0, "Meter location or reference"), Cell(0, 1, "Borehole 1", left: 100),
                Cell(1, 0, "Serial number"), Cell(1, 1, "AAA", left: 110),
                Cell(2, 0, "Meter location or reference"), Cell(2, 1, "Borehole 2", left: 120),
                Cell(3, 0, "Serial number"), Cell(3, 1, "BBB", left: 130))
        ],
            "Meter location or reference");

        Assert.Equal(2, groups.Count);
        Assert.Equal(110, KeyValueTableHelper.FindValue(groups[0], "Serial number")!.Coordinates!.Left);
        Assert.Equal(130, KeyValueTableHelper.FindValue(groups[1], "Serial number")!.Coordinates!.Left);
    }

    [Fact]
    public void WhenOverlappingTablesRepeatAGroup_ThenTheMergeKeepsTheWinningCopysRegion()
    {
        // Five boreholes came out as ten records before the merge existed; the copies disagree,
        // so the merge takes the longer value per key - and must take its region with it.
        var groups = KeyValueTableHelper.ReadRepeatingGroups(
        [
            Table(
                Cell(0, 0, "Meter location or reference"), Cell(0, 1, "Borehole 1", left: 100),
                Cell(1, 0, "Serial number"), Cell(1, 1, "AAA", left: 110)),
            Table(
                Cell(0, 0, "Meter location or reference"), Cell(0, 1, "Borehole 1", left: 200),
                Cell(1, 0, "Serial number"), Cell(1, 1, "AAA-12345", left: 210))
        ],
            "Meter location or reference");

        var group = Assert.Single(groups);
        var serial = KeyValueTableHelper.FindValue(group, "Serial number");

        Assert.Equal("AAA-12345", serial!.Text);
        Assert.Equal(210, serial.Coordinates!.Left);
    }

    [Fact]
    public void WhenATableIsNotTwoColumns_ThenItContributesNothing()
    {
        var table = new DocumentTable
        {
            ColumnCount = 3,
            RowCount = 1,
            Cells = [Cell(0, 0, "Licence number"), Cell(0, 1, "1/2/3", left: 10), Cell(0, 2, "extra")]
        };

        Assert.Empty(KeyValueTableHelper.ReadPairs([table]));
    }
}
