using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Unit coverage for TableMatcherHelper.MatchMultiValueTextFields - the real WR51 multi-meter
/// bug this exists to fix: MatchTextFields only ever returns the FIRST matching cell for a
/// field, so a document with several meters (each its own row) silently picks one meter's value
/// at random and discards the rest, and the schema converter's own BuildMeters (which expects
/// one LabelGroupResult.Text line per meter) never gets the shape it needs. Fixtures mirror the
/// real cell shape confirmed on wr51__940030021sr: two separate rows, each with a label+value
/// pair merged into one cell, "Meter make:" appearing mid-cell (not a leading prefix) on one of
/// them.
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
        // "Meter make:" here is NOT a prefix of the cell - it follows an unrelated sentence.
        // FindTextValueInTable's own StartsWith-only check would miss this entirely.
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
        // Multi-page documents (see PdfClownGridTableExtractorService's own multi-page support)
        // return one DocumentTable per page - a meter's row on a later page must not jump ahead
        // of an earlier page's meter just because it's a lower RowIndex on its own page.
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

        // Deliberately passed page 2 before page 1 - both rows tie on RowIndex 0 (one row per
        // table), so a naive RowIndex-only sort would fall back to list/table order and put
        // SecondPageMeter first. Ordering by PageNumber first is what makes this deterministic
        // regardless of the tables list's own order.
        var results = TableMatcherHelper.MatchMultiValueTextFields(
            [tablePage2, tablePage1], Labels, "TestTableService", MeterFieldBoundaryLabels);

        var values = results[WrInspectionReportFieldNames.MeterMake].Text!.Select(t => t.Text).ToList();
        Assert.Equal(["FirstPageMeter", "SecondPageMeter"], values);
    }

    [Fact]
    public void WhenAnEarlierPagesMeterHasAHigherRowIndexThanALaterPagesMeter_ThenPageOrderStillWins()
    {
        // The actual bug this guards, not just a RowIndex tie: BuildDocumentTable resets
        // RowIndex to 0 at the top of every page's own grid, so a real multi-page document's
        // page 2 meter can easily land on a LOWER RowIndex than a page 1 meter that has several
        // other rows above it - sorting by RowIndex alone (ignoring PageNumber) would put the
        // page 2 meter first, scrambling which meter's fields get zipped together downstream.
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
        // The real bug this guards: a photo-caption cell reading "Abstraction meter showing
        // asset and serial numbers" (plural) case-insensitively contains "serial number" as a
        // literal substring - stripping that 13-char match left just the stray trailing "s"
        // from "numbers". Confirmed via the golden set (three real WR51 documents, all
        // producing the literal value "s").
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
        // The other real WR51 cell shape (wr51__1041260103): a bare label with nothing else in
        // its own cell, value in a separate, later column of the same row.
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
        // The other real bug this guards: "serial number" (or similar) appearing as a genuine,
        // word-bounded standalone occurrence inside a long licence-condition paragraph, with no
        // sibling field label anywhere in it to bound the value - the whole rest of that
        // paragraph would otherwise be captured as if it were the serial number.
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
