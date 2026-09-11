using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;
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

    private static DocumentTableCell Cell(int row, int col, string content) => new()
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

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

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

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

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

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

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

        var table = new DocumentTable { RowCount = 5, ColumnCount = 3, Cells = cells };

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

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [table], Labels, GridFieldNames, "TestTableService");

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

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [headerTable, gridTable], Labels, GridFieldNames, "TestTableService");

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

        var results = WrInspectionReportTableMatcher.MatchGridFields(
            [unrelatedTable], Labels, GridFieldNames, "TestTableService");

        Assert.Empty(results);
    }

    // Builds a table containing all 13 grid fields (merged label:value cells), matching the
    // real shape confirmed on 16/17 tested documents during prototyping - lets individual
    // tests override one field's value without repeating the whole grid each time.
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

    // MatchFreeTextFields - the sibling to MatchGridFields for Time/SerialNumber/TelephoneNumber:
    // no Possibilities matching, the raw cell remainder (or split-cell next cell) IS the value.

    private static readonly IReadOnlyList<string> FreeTextFieldNames =
    [
        WrInspectionReportFieldNames.Time, WrInspectionReportFieldNames.SerialNumber,
        WrInspectionReportFieldNames.TelephoneNumber
    ];

    [Fact]
    public void WhenFreeTextFieldsSitInTheSameTableAsTheGrid_ThenTheyAreExtracted()
    {
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Append(Cell(5, 0, "Telephone No:   02380891203"))
            .Append(Cell(5, 1, "Time: 10:00"))
            .Append(Cell(6, 0, "Serial number: R2116126"))
            .ToList();

        var table = new DocumentTable { RowCount = 7, ColumnCount = 3, Cells = cells };

        var results = WrInspectionReportTableMatcher.MatchFreeTextFields(
            [table], Labels, GridFieldNames, FreeTextFieldNames, "TestTableService");

        Assert.Equal("02380891203", results[WrInspectionReportFieldNames.TelephoneNumber].Text!.Single().Text);
        Assert.Equal("10:00", results[WrInspectionReportFieldNames.Time].Text!.Single().Text);
        Assert.Equal("R2116126", results[WrInspectionReportFieldNames.SerialNumber].Text!.Single().Text);
        Assert.All(results.Values, r => Assert.Equal("TestTableService", r.ServiceName));
    }

    [Fact]
    public void WhenAFreeTextFieldIsGenuinelyAbsent_ThenItIsNotInTheResults()
    {
        // Grid present (passes the table-selection gate) but no Telephone No/Time/Serial number
        // cells at all - a real, if less common, real-world shape (e.g. a document where the
        // header block itself isn't part of the same table Lattice detected for the grid).
        var table = BuildFullGridTable(specialConditionsValue: "✓");

        var results = WrInspectionReportTableMatcher.MatchFreeTextFields(
            [table], Labels, GridFieldNames, FreeTextFieldNames, "TestTableService");

        Assert.Empty(results);
    }

    [Fact]
    public void WhenNoTableResemblesTheGrid_ThenFreeTextFieldsAreNotResolvedEitherEvenIfPresentElsewhere()
    {
        // FindBestGridTable's own majority-of-grid gate applies here too - a table with real
        // Telephone No/Time/Serial number cells but nothing resembling the LicenceProvisions grid
        // itself must not be trusted, same reasoning as MatchGridFields's own equivalent guard.
        var unrelatedTable = new DocumentTable
        {
            RowCount = 1,
            ColumnCount = 2,
            Cells = [Cell(0, 0, "Telephone No: 07794218297"), Cell(0, 1, "Time: 09:00")]
        };

        var results = WrInspectionReportTableMatcher.MatchFreeTextFields(
            [unrelatedTable], Labels, GridFieldNames, FreeTextFieldNames, "TestTableService");

        Assert.Empty(results);
    }

    [Fact]
    public void WhenSerialNumberUsesATemplateAlternateWordingRatherThanTheFirst_ThenItIsStillFound()
    {
        // SerialNumber has 3 real alternates (Existing/T6/Baseline) - MatchFreeTextFields must
        // try each alternate's own TextStart, not just the first ("Serial number").
        var cells = BuildFullGridTable(specialConditionsValue: "✓").Cells
            .Append(Cell(5, 0, "Meter Serial Number: 3K220000854902"))
            .ToList();

        var table = new DocumentTable { RowCount = 6, ColumnCount = 3, Cells = cells };

        var results = WrInspectionReportTableMatcher.MatchFreeTextFields(
            [table], Labels, GridFieldNames, FreeTextFieldNames, "TestTableService");

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

        var results = WrInspectionReportTableMatcher.MatchFreeTextFields(
            [table], Labels, GridFieldNames, FreeTextFieldNames, "TestTableService");

        Assert.Equal("07794218297", results[WrInspectionReportFieldNames.TelephoneNumber].Text!.Single().Text);
    }
}