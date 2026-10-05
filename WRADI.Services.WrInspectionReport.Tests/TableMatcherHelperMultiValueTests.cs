using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Unit coverage for TableMatcherHelper.MatchMultiValueTextFields. The bug it fixes:
/// MatchTextFields returns only the FIRST matching cell, so a multi-meter document keeps one
/// meter's value at random and discards the rest, and BuildMeters never gets the one-line-per-meter
/// shape it expects. Fixtures mirror wr51__940030021sr: two rows, each a label+value pair merged
/// into one cell, with "Meter make:" mid-cell rather than leading on one of them.
/// </summary>
public class TableMatcherHelperMultiValueTests
{
    private static readonly IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> Labels =
        WrInspectionReportTextBasedLabelConfiguration.GetLabels();

    private static readonly string[] MeterFieldBoundaryLabels =
    [
        "Meter Name", "Meter make", "Serial number", "Meter Serial Number", "Meter Asset Number",
        "Reading", "Flow Rate", "Units"
    ];

    private static DocumentTableCell Cell(int row, int col, string content) => new()
    {
        RowIndex = row,
        ColumnIndex = col,
        Content = content
    };

    [Fact]
    public void WhenTwoMetersEachHaveTheirOwnRow_ThenBothValuesAreReturnedInRowOrder()
    {
        var table = new DocumentTable
        {
            RowCount = 2,
            ColumnCount = 1,
            Cells =
            [
                Cell(0, 0, "Meter at previous site visit 25th March 2025 Meter make: ARAD Serial number: 20-100019688"),
                Cell(1, 0, "Meter make: VuAqua Serial number 25 061010")
            ]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        var meterMake = results[WrInspectionReportFieldNames.MeterMake];
        Assert.Equal(2, meterMake.Text!.Count);
        Assert.Equal("ARAD", meterMake.Text[0].Text);
        Assert.Equal("VuAqua", meterMake.Text[1].Text);
    }

    [Fact]
    public void WhenLabelAppearsMidCell_ThenItIsStillFound_NotOnlyAsALeadingPrefix()
    {
        // "Meter make:" follows an unrelated sentence rather than leading the cell, which
        // FindTextValueInTable's StartsWith-only check misses entirely.
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter at previous site visit 25th March 2025 Meter make: ARAD Serial number: 20-100019688")]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        Assert.Equal("ARAD", results[WrInspectionReportFieldNames.MeterMake].Text!.Single().Text);
    }

    [Fact]
    public void WhenASiblingFieldLabelFollowsInTheSameCell_ThenTheValueStopsBeforeIt()
    {
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: VuAqua Serial number 25 061010")]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        Assert.Equal("VuAqua", results[WrInspectionReportFieldNames.MeterMake].Text!.Single().Text);
        Assert.Equal("25 061010", results[WrInspectionReportFieldNames.SerialNumber].Text!.Single().Text);
    }

    [Fact]
    public void WhenOnlyOneMeterExists_ThenASingleLineResultIsStillProduced()
    {
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: Honeywell")]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        var meterMake = results[WrInspectionReportFieldNames.MeterMake];
        Assert.Single(meterMake.Text!);
        Assert.Equal("Honeywell", meterMake.Text![0].Text);
    }

    [Fact]
    public void WhenTheFieldIsGenuinelyAbsent_ThenItIsNotInTheResults()
    {
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Some unrelated content")]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        Assert.False(results.ContainsKey(WrInspectionReportFieldNames.MeterMake));
    }

    [Fact]
    public void WhenMetersSpanMultipleTables_ThenValuesAreStillOrderedByRowAcrossThem()
    {
        // One DocumentTable per page, so a later page's meter must not jump ahead of an earlier
        // page's just because it has a lower RowIndex within its own table.
        var tablePage2 = new DocumentTable
        {
            PageNumber = 2,
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: SecondPageMeter")]
        };
        var tablePage1 = new DocumentTable
        {
            PageNumber = 1,
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: FirstPageMeter")]
        };

        // Page 2 passed before page 1, and both rows tie on RowIndex 0, so a RowIndex-only sort
        // falls back to table order and puts SecondPageMeter first.
        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [tablePage2, tablePage1], Labels, "TestTableService", MeterFieldBoundaryLabels);

        var values = results[WrInspectionReportFieldNames.MeterMake].Text!.Select(t => t.Text).ToList();
        Assert.Equal(["FirstPageMeter", "SecondPageMeter"], values);
    }

    [Fact]
    public void WhenAnEarlierPagesMeterHasAHigherRowIndexThanALaterPagesMeter_ThenPageOrderStillWins()
    {
        // The bug behind the tie in the test above: BuildDocumentTable restarts RowIndex at 0 per
        // page, so a page 2 meter can genuinely hold a LOWER RowIndex than a page 1 meter with rows
        // above it, scrambling which meter's fields get zipped together downstream.
        var tablePage1 = new DocumentTable
        {
            PageNumber = 1,
            RowCount = 4,
            ColumnCount = 1,
            Cells =
            [
                Cell(0, 0, "Unrelated header row"),
                Cell(1, 0, "Another unrelated row"),
                Cell(2, 0, "Yet another row"),
                Cell(3, 0, "Meter make: FirstPageMeter")
            ]
        };
        var tablePage2 = new DocumentTable
        {
            PageNumber = 2,
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: SecondPageMeter")]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [tablePage1, tablePage2], Labels, "TestTableService", MeterFieldBoundaryLabels);

        var values = results[WrInspectionReportFieldNames.MeterMake].Text!.Select(t => t.Text).ToList();
        Assert.Equal(["FirstPageMeter", "SecondPageMeter"], values);
    }

    [Fact]
    public void WhenNoBoundaryLabelFollows_ThenTheWholeRemainderIsTheValue()
    {
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: Honeywell Flowmeter")]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        Assert.Equal("Honeywell Flowmeter", results[WrInspectionReportFieldNames.MeterMake].Text!.Single().Text);
    }

    [Fact]
    public void WhenTheLabelWordIsEmbeddedInsideAnUnrelatedLongerWord_ThenItIsNotMatched()
    {
        // The real bug: a photo caption "Abstraction meter showing asset and serial numbers"
        // contains "serial number" as a substring, and stripping that match left the stray "s".
        // Three golden-set documents all produced the literal value "s".
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Abstraction meter showing asset and serial numbers")]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        Assert.False(results.ContainsKey(WrInspectionReportFieldNames.SerialNumber));
    }

    [Fact]
    public void WhenLabelAndValueAreSplitAcrossAdjacentCells_ThenTheNextCellIsUsed()
    {
        // The other real cell shape (wr51__1041260103): a bare label alone in its cell, value in
        // a later column of the same row.
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 4,
            Cells =
            [
                Cell(0, 0, "Meter Serial Number"),
                Cell(0, 3, "N1M3035020")
            ]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        Assert.Equal("N1M3035020", results[WrInspectionReportFieldNames.SerialNumber].Text!.Single().Text);
    }

    [Fact]
    public void WhenTwoMetersAreBothSplitAcrossAdjacentCells_ThenBothValuesAreReturnedInRowOrder()
    {
        var table = new DocumentTable
        {
            RowCount = 2,
            ColumnCount = 4,
            Cells =
            [
                Cell(0, 0, "Meter Serial Number"),
                Cell(0, 3, "N1M3035020"),
                Cell(1, 0, "Meter Serial Number"),
                Cell(1, 3, "N1LD135232")
            ]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        var values = results[WrInspectionReportFieldNames.SerialNumber].Text!.Select(t => t.Text).ToList();
        Assert.Equal(["N1M3035020", "N1LD135232"], values);
    }

    [Fact]
    public void WhenNoBoundaryTermStopsAnImplausiblyLongRemainder_ThenItIsNotTrusted()
    {
        // The other real bug: a word-bounded "serial number" inside a long licence-condition
        // paragraph with no sibling label to bound it would capture the rest of the paragraph.
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells =
            [
                Cell(0, 0, "This licence requires that meters have a valid serial number recorded and that " +
                           "the operator maintains accurate records of abstraction at all times as set out " +
                           "in condition 9.2 of Schedule B.")
            ]
        };

        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [table], Labels, "TestTableService", MeterFieldBoundaryLabels);

        Assert.False(results.ContainsKey(WrInspectionReportFieldNames.SerialNumber));
    }
}
