using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Helpers;
namespace WRADI.Services.WrInspectionReport.Tests;

/// Hand-crafted fixtures, no live Azure calls. Both cell shapes Document Intelligence produces
/// are covered: label and value merged into one cell, the majority, and split into adjacent cells
/// (wr51__nw0690016005).
public class TableMatcherHelperTests
{
    private static readonly IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> Labels =
        WrInspectionReportTextBasedLabelConfiguration.GetLabels();

    private static DocumentTableCell Cell(int row, int col, string content) => new()
    {
        RowIndex = row,
        ColumnIndex = col,
        Content = content
    };

    /// Cell() above sets no rectangle, and ToCoordinates() needs all four edges, so a region can
    /// only be asserted against a cell built here.
    private static DocumentTableCell CellAt(int row, int col, string content, double left) => new()
    {
        RowIndex = row,
        ColumnIndex = col,
        Content = content,
        Left = left,
        Top = 10,
        Right = left + 40,
        Bottom = 20
    };

    private static DocumentLineWordCoordinates RegionOf(LabelGroupResult result) =>
        result.Text!.Single().Columns!.First().Words!.First().Coordinates;

    [Fact]
    public void WhenLabelAndValueAreMergedInOneCell_ThenValueIsExtracted()
    {
        // A full grid, not a bare 2-cell table: FindBestGridTable requires a majority-of-grid
        // match, so a minimal fixture is rejected before reaching the logic under test.
        var table = BuildFullGridTable(specialConditionsValue: "✓");

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        var sourceOfSupply = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SourceOfSupply).Value;
        Assert.Equal("✓", sourceOfSupply.Text!.Single().Text);
        Assert.Equal(WrInspectionReportFieldNames.SourceOfSupply, sourceOfSupply.MatchedLabelName);
        Assert.Equal("TestTableService", sourceOfSupply.ServiceName);
    }

    [Fact]
    public void WhenCellContentIsAzuresOwnSelectionMarkAnnotation_ThenItIsNormalisedToARealTick()
    {
        // Azure writes ":selected:" into Content for checkbox cells, often with no tick glyph at
        // all. Unhandled, it falls through to the "" catch-all and scores Blank.
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Where(c => c.RowIndex != 0 || c.ColumnIndex is not (0 or 1))
            .Append(Cell(0, 0, "Source of supply:"))
            .Append(Cell(0, 1, ":selected:"))
            .ToList();

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        var sourceOfSupply = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SourceOfSupply).Value;
        Assert.Equal("✓", sourceOfSupply.Text!.Single().Text);
    }

    [Fact]
    public void WhenCellContentIsGenuinelyUnrecognisable_ThenFieldResolvesAsBlankViaTheCatchAll()
    {
        // Content found, short enough to be a tick, but matching no Possibility resolves as Blank
        // via the "" catch-all rather than falling back to the heuristic.
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Where(c => c.RowIndex != 0 || c.ColumnIndex is not (0 or 1))
            .Append(Cell(0, 0, "Source of supply:"))
            .Append(Cell(0, 1, "???")) // Short, but not a recognised glyph/word
            .ToList();

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        var sourceOfSupply = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SourceOfSupply).Value;
        Assert.Equal(string.Empty, sourceOfSupply.Text!.Single().Text);
    }

    [Fact]
    public void WhenCellContentIsNarrativeLength_ThenFieldFallsBackRatherThanFabricatingAStatus()
    {
        // A prose answer shares the label's cell: "Source of supply: Lower Greensand at Warwick
        // Wold / Brewer St :selected:". Without the guard, ":selected:" normalises to "✓" and
        // Contains("✓") fabricates InOrder for a field the model cannot represent at all.
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Where(c => c.RowIndex != 0 || c.ColumnIndex is not (0 or 1))
            .Append(Cell(0, 0, "Source of supply: Lower Greensand at Warwick Wold / Brewer St :selected:"))
            .Append(Cell(0, 1, "Quantities: ✓"))
            .ToList();

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        Assert.False(results.ContainsKey(WrInspectionReportFieldNames.SourceOfSupply));
        // Per-field, so a short legitimate answer elsewhere in the row still uses the table.
        Assert.True(results.ContainsKey(WrInspectionReportFieldNames.Quantities));
    }

    [Fact]
    public void WhenSplitCellsUseARealTickGlyph_ThenValueIsExtracted()
    {
        var cells = BuildFullGridTable(specialConditionsValue: string.Empty).Cells
            .Where(c => !(c.RowIndex == 4 && c.ColumnIndex == 1))
            .Append(Cell(4, 1, "Special conditions:"))
            .Append(Cell(4, 2, "N/A"))
            .ToList();

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        var specialConditions = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SpecialConditions).Value;
        Assert.Equal("N/A", specialConditions.Text!.Single().Text);
    }

    [Fact]
    public void WhenCellContentIsGenuinelyBlank_ThenFieldResolvesToBlankNotAFallback()
    {
        var table = BuildFullGridTable(specialConditionsValue: string.Empty);

        var l = new List<(string LabelGroupName, List<LabelToMatch> Labels)>
        {
            ("SpecialConditions", Labels.Where(x => x.LabelGroupName == "SpecialConditions").SelectMany(x => x.Labels).ToList())
        };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], l, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        var specialConditions = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SpecialConditions).Value;
        // The "" catch-all resolves downstream to a confident Blank, not a miss.
        Assert.Equal(string.Empty, specialConditions.Text!.Single().Text);
    }

    [Fact]
    public void WhenFieldLabelIsNotFoundInAnyTable_ThenFieldIsAbsentFromResultsSoCallerFallsBack()
    {
        var table = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 2,
            Cells =
            [
                Cell(0, 0, "Some unrelated header:"),
                Cell(0, 1, "value")
            ]
        };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        Assert.Empty(results);
    }

    [Fact]
    public void WhenMultipleTablesArePresent_ThenTheOneMatchingTheMostGridLabelsIsChosen()
    {
        var headerTable = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 2,
            Cells =
            [
                Cell(0, 0, "Licence No: 1/2/3"),
                Cell(0, 1, "Inspection Class:")
            ]
        };

        var gridTable = BuildFullGridTable(specialConditionsValue: "✓");

        var results = TableMatcherHelper.MatchToPossibilities(
            [headerTable, gridTable], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        Assert.True(results.ContainsKey(WrInspectionReportFieldNames.SpecialConditions));
        Assert.Equal("✓", results[WrInspectionReportFieldNames.SpecialConditions].Text!.Single().Text);
    }

    [Fact]
    public void WhenNoTableResemblesTheGridAtAll_ThenNoFieldsAreResolved()
    {
        var unrelatedTable = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: ABB")]
        };

        var results = TableMatcherHelper.MatchToPossibilities(
            [unrelatedTable], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        Assert.Empty(results);
    }

    // All 13 grid fields as merged label:value cells, the shape 16 of 17 tested documents use.
    private static DocumentTable BuildFullGridTable(string specialConditionsValue)
    {
        return new DocumentTable
        {
            RowCount = 5,
            ColumnCount = 3,
            Cells =
            [
                Cell(0, 0, "Source of supply: ✓"),
                Cell(0, 1, "Quantities: ✓"),
                Cell(0, 2, "Land (only if specified): N/A"),
                Cell(1, 0, "Point of abstraction: ✓"),
                Cell(1, 1, "Means of measurement: ✓"),
                Cell(1, 2, "Charging factors: N/A"),
                Cell(2, 0, "Means of abstraction: ✓"),
                Cell(2, 1, "Records: ✓"),
                Cell(2, 2, "Other provisions (specify below): N/A"),
                Cell(3, 0, "Purpose(s): ✓"),
                Cell(3, 1, "Provision of information: ✓"),
                Cell(4, 0, "Period: ✓"),
                Cell(4, 1, $"Special conditions: {specialConditionsValue}")
            ]
        };
    }
    
    [Fact]
    public void WhenFreeTextFieldsSitInTheSameTableAsTheGrid_ThenTheyAreExtracted()
    {
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Append(Cell(5, 0, "Telephone No:   02380891203"))
            .Append(Cell(5, 1, "Time: 10:00"))
            .Append(Cell(6, 0, "Serial number: R2116126"))
            .ToList();

        var table = new DocumentTable { RowCount = 7, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchTextFields(
            [table], Labels, "TestTableService");

        Assert.Equal("02380891203", results[WrInspectionReportFieldNames.TelephoneNumber].Text!.Single().Text);
        Assert.Equal("10:00", results[WrInspectionReportFieldNames.Time].Text!.Single().Text);
        Assert.Equal("R2116126", results[WrInspectionReportFieldNames.SerialNumber].Text!.Single().Text);
        Assert.All(results.Values, r => Assert.Equal("TestTableService", r.ServiceName));
    }

    [Fact]
    public void WhenAFreeTextFieldIsGenuinelyAbsent_ThenItIsNotInTheResults()
    {
        // A real shape: the grid passes the selection gate but the header block is not part of
        // the same table, so there are no Telephone No/Time/Serial number cells.
        var table = BuildFullGridTable(specialConditionsValue: "✓");

        var results = TableMatcherHelper.MatchTextFields(
            [table], Labels, "TestTableService");

        Assert.Empty(results);
    }

    /*[Fact] Don't think this reasoning holds up so commenting out for now
    public void WhenNoTableResemblesTheGrid_ThenFreeTextFieldsAreNotResolvedEitherEvenIfPresentElsewhere()
    {
            // The majority-of-grid gate applies here too, so a table with header cells but no
            // provisions grid is not trusted.
        var unrelatedTable = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 2,
            Cells = [Cell(0, 0, "Telephone No: 07794218297"), Cell(0, 1, "Time: 09:00")]
        };

        var results = WrInspectionReportTableMatcher.MatchFreeTextFields(
            [unrelatedTable], Labels, GridFieldNames, FreeTextFieldNames, "TestTableService");

        Assert.Empty(results);
    }*/

    [Fact]
    public void WhenSerialNumberUsesATemplateAlternateWordingRatherThanTheFirst_ThenItIsStillFound()
    {
        // SerialNumber has three alternates, so each one's TextStart must be tried, not just the
        // first.
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Append(Cell(5, 0, "Meter Serial Number: 3K220000854902"))
            .ToList();

        var table = new DocumentTable { RowCount = 6, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchTextFields(
            [table], Labels, "TestTableService");

        Assert.Equal("3K220000854902", results[WrInspectionReportFieldNames.SerialNumber].Text!.Single().Text);
    }

    [Fact]
    public void WhenAFreeTextLabelAndValueAreSplitAcrossAdjacentCells_ThenTheNextCellIsUsed()
    {
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Append(Cell(5, 0, "Telephone No:"))
            .Append(Cell(5, 1, "07794218297"))
            .ToList();

        var table = new DocumentTable { RowCount = 6, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchTextFields(
            [table], Labels, "TestTableService");

        Assert.Equal("07794218297", results[WrInspectionReportFieldNames.TelephoneNumber].Text!.Single().Text);
    }

    [Fact]
    public void WhenGridLabelAndValueShareACell_ThenTheMatchCarriesThatCellsRegion()
    {
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Where(c => c.RowIndex != 0 || c.ColumnIndex != 0)
            .Append(CellAt(0, 0, "Source of supply: ✓", left: 300))
            .ToList();

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        Assert.Equal(300, RegionOf(results[WrInspectionReportFieldNames.SourceOfSupply]).Left);
    }

    [Fact]
    public void WhenGridLabelAndValueAreSplit_ThenTheRegionIsTheValueCellsNotTheLabelCells()
    {
        // The label cell is the one iterated, so its region would outline the printed question
        // rather than the answer.
        var cells = BuildFullGridTable(specialConditionsValue: string.Empty).Cells
            .Where(c => !(c.RowIndex == 4 && c.ColumnIndex == 1))
            .Append(CellAt(4, 1, "Special conditions:", left: 300))
            .Append(CellAt(4, 2, "N/A", left: 600))
            .ToList();

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchToPossibilities(
            [table], Labels, "TestTableService",
            TickHelper.GetTickedOrAcceptedStatus);

        Assert.Equal(600, RegionOf(results[WrInspectionReportFieldNames.SpecialConditions]).Left);
    }

    [Fact]
    public void WhenTextFieldLabelAndValueShareACell_ThenTheMatchCarriesThatCellsRegion()
    {
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Append(CellAt(5, 0, "Telephone No:   02380891203", left: 120))
            .ToList();

        var table = new DocumentTable { RowCount = 6, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchTextFields([table], Labels, "TestTableService");

        Assert.Equal(120, RegionOf(results[WrInspectionReportFieldNames.TelephoneNumber]).Left);
    }

    [Fact]
    public void WhenTextFieldLabelAndValueAreSplit_ThenTheRegionIsTheValueCellsNotTheLabelCells()
    {
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Append(CellAt(5, 0, "Telephone No:", left: 120))
            .Append(CellAt(5, 1, "07794218297", left: 480))
            .ToList();

        var table = new DocumentTable { RowCount = 6, ColumnCount = 3, Cells = cells };

        var results = TableMatcherHelper.MatchTextFields([table], Labels, "TestTableService");

        Assert.Equal(480, RegionOf(results[WrInspectionReportFieldNames.TelephoneNumber]).Left);
    }
}
