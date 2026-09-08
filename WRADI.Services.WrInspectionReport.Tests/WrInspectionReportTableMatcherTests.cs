using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Unit coverage for WrInspectionReportTableMatcher, built from hand-crafted OcrTable/
/// OcrTableCell fixtures - no live Azure calls, mirroring the existing
/// FindLabelGroupMatchesHelperColumnTests.cs pattern for the heuristic column-walk mechanisms.
/// Covers both cell shapes found during real prototyping against Azure AI Document
/// Intelligence's prebuilt-layout model: label+value merged into one cell (the majority of
/// documents), and label/value split into adjacent cells (wr51__nw0690016005).
/// </summary>
public class WrInspectionReportTableMatcherTests
{
    private static readonly IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> Labels =
        WrInspectionReportLabelConfiguration.GetLabels();

    private static readonly IReadOnlyList<string> GridFieldNames =
    [
        WrInspectionReportFieldNames.SourceOfSupply, WrInspectionReportFieldNames.PointOfAbstraction,
        WrInspectionReportFieldNames.MeansOfAbstraction, WrInspectionReportFieldNames.Purposes,
        WrInspectionReportFieldNames.Period, WrInspectionReportFieldNames.Quantities,
        WrInspectionReportFieldNames.MeansOfMeasurement, WrInspectionReportFieldNames.Records,
        WrInspectionReportFieldNames.ProvisionOfInformation, WrInspectionReportFieldNames.SpecialConditions,
        WrInspectionReportFieldNames.Land, WrInspectionReportFieldNames.ChargingFactors,
        WrInspectionReportFieldNames.OtherProvisions
    ];

    private static OcrTableCell Cell(int row, int col, string content) => new()
    {
        RowIndex = row,
        ColumnIndex = col,
        Content = content
    };

    [Fact]
    public void WhenLabelAndValueAreMergedInOneCell_ThenValueIsExtracted()
    {
        // Embedded in a full grid, not a bare 2-cell table - FindBestGridTable deliberately
        // requires a majority-of-the-grid match before trusting a table at all (guards against
        // a coincidental one- or two-label overlap with an unrelated block), so a minimal
        // fixture would be rejected before ever reaching the per-field logic under test here.
        var table = BuildFullGridTable(specialConditionsValue: "✓");

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

        var sourceOfSupply = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SourceOfSupply).Value;
        Assert.Equal("✓", sourceOfSupply.Text!.Single().Text);
        Assert.Equal(WrInspectionReportFieldNames.SourceOfSupply, sourceOfSupply.MatchedLabelName);
        Assert.Equal("TestTableService", sourceOfSupply.ServiceName);
    }

    [Fact]
    public void WhenCellContentIsAzuresOwnSelectionMarkAnnotation_ThenItIsNormalisedToARealTick()
    {
        // Azure's own ":selected:"/":unselected:" selection-mark annotation appears directly in
        // Content for checkbox-style cells, often WITHOUT a redundant rendered tick glyph. Left
        // unhandled, this falls through to the "" catch-all Possibility and scores Blank instead
        // of the real tick - locks in the fix (NormaliseSelectionMarks).
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Where(c => c.RowIndex != 0 || c.ColumnIndex is not (0 or 1))
            .Append(Cell(0, 0, "Source of supply:"))
            .Append(Cell(0, 1, ":selected:"))
            .ToList();

        var table = new OcrTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

        var sourceOfSupply = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SourceOfSupply).Value;
        Assert.Equal("✓", sourceOfSupply.Text!.Single().Text);
    }

    [Fact]
    public void WhenCellContentIsGenuinelyUnrecognisable_ThenFieldResolvesAsBlankViaTheCatchAll()
    {
        // A field only falls back to the heuristic when its label isn't found in the table AT
        // ALL, or when its content is too long to plausibly be a tick answer (see
        // WhenCellContentIsNarrativeLength... below) - see WhenFieldLabelIsNotFoundInAnyTable
        // too. Content that IS found, IS short enough to be a tick, but matches no real
        // Possibility still resolves as Blank via InOrderPossibilities' "" catch-all - same design
        // the heuristic column-walk path already relies on (see that Possibility's own comment -
        // "a genuinely blank tick field must still survive as a match").
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Where(c => c.RowIndex != 0 || c.ColumnIndex is not (0 or 1))
            .Append(Cell(0, 0, "Source of supply:"))
            .Append(Cell(0, 1, "???")) // Short, but not a recognised glyph/word
            .ToList();

        var table = new OcrTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

        var sourceOfSupply = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SourceOfSupply).Value;
        Assert.Equal(string.Empty, sourceOfSupply.Text!.Single().Text);
    }

    [Fact]
    public void WhenCellContentIsNarrativeLength_ThenFieldFallsBackRatherThanFabricatingAStatus()
    {
        // Narrative-answer documents put a genuine prose answer in the SAME cell as the label,
        // e.g. "Source of supply: Lower Greensand at Warwick Wold / Brewer St :selected:".
        // Without this guard, normalising ":selected:" to "✓" matches the InOrder Possibility
        // via Contains("✓") on the whole sentence - fabricating "InOrder" for a field the model
        // has no way to represent at all. Falling back to the heuristic is strictly safer, even
        // though it can't represent narrative answers either - it won't confidently guess wrong.
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Where(c => c.RowIndex != 0 || c.ColumnIndex is not (0 or 1))
            .Append(Cell(0, 0, "Source of supply: Lower Greensand at Warwick Wold / Brewer St :selected:"))
            .Append(Cell(0, 1, "Quantities: ✓"))
            .ToList();

        var table = new OcrTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

        Assert.False(results.ContainsKey(WrInspectionReportFieldNames.SourceOfSupply));
        // Confirms this is a targeted, per-field guard, not an overreaction that also rejects
        // the table for genuinely short, legitimate answers on other fields in the same row.
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

        var table = new OcrTable { RowCount = 5, ColumnCount = 3, Cells = cells };

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

        var specialConditions = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SpecialConditions).Value;
        Assert.Equal("N/A", specialConditions.Text!.Single().Text);
    }

    [Fact]
    public void WhenCellContentIsGenuinelyBlank_ThenFieldResolvesToBlankNotAFallback()
    {
        var table = BuildFullGridTable(specialConditionsValue: string.Empty);

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

        var specialConditions = Assert.Single(results, r => r.Key == WrInspectionReportFieldNames.SpecialConditions).Value;
        // Matches the "" catch-all Possibility (see InOrderPossibilities) - resolves to a real,
        // confident Blank verdict downstream (WrInspectionReportSchemaConverter.
        // GetInOrderStatus treats an empty/whitespace-only joined Text as InOrderStatus.Blank),
        // not a miss.
        Assert.Equal(string.Empty, specialConditions.Text!.Single().Text);
    }

    [Fact]
    public void WhenFieldLabelIsNotFoundInAnyTable_ThenFieldIsAbsentFromResultsSoCallerFallsBack()
    {
        var table = new OcrTable
        {
            RowCount = 1,
            ColumnCount = 2,
            Cells =
            [
                Cell(0, 0, "Some unrelated header:"),
                Cell(0, 1, "value")
            ]
        };

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

        Assert.Empty(results);
    }

    [Fact]
    public void WhenMultipleTablesArePresent_ThenTheOneMatchingTheMostGridLabelsIsChosen()
    {
        var headerTable = new OcrTable
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

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [headerTable, gridTable], Labels, GridFieldNames, "TestTableService");

        Assert.True(results.ContainsKey(WrInspectionReportFieldNames.SpecialConditions));
        Assert.Equal("✓", results[WrInspectionReportFieldNames.SpecialConditions].Text!.Single().Text);
    }

    [Fact]
    public void WhenNoTableResemblesTheGridAtAll_ThenNoFieldsAreResolved()
    {
        var unrelatedTable = new OcrTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [Cell(0, 0, "Meter make: ABB")]
        };

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [unrelatedTable], Labels, GridFieldNames, "TestTableService");

        Assert.Empty(results);
    }

    // Builds a table containing all 13 grid fields (merged label:value cells), matching the
    // real shape confirmed on 16/17 tested documents during prototyping - lets individual
    // tests override one field's value without repeating the whole grid each time.
    private static OcrTable BuildFullGridTable(string specialConditionsValue)
    {
        return new OcrTable
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
}
