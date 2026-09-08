using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Services;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Verifies the fallback-safe merge in WrInspectionReportExtractionOrchestrator.
/// ApplyTableBasedGridMatchesAsync: fields the table lookup resolves confidently REPLACE the
/// heuristic result; every other field (grid fields the table couldn't resolve, and every
/// non-grid field) keeps its existing heuristic result completely untouched. No live Azure
/// calls or real PDF needed - ITableExtractorService is faked.
/// </summary>
public class WrInspectionReportExtractionOrchestratorTableMergeTests
{
    private static readonly List<(string LabelGroupName, List<LabelToMatch> Labels)> Labels =
        WrInspectionReportLabelConfiguration.GetLabels();

    private class FakeTableExtractorService(IReadOnlyList<OcrTable> tables) : ITableExtractorService
    {
        public string Name => "FakeTableExtractorService";
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<OcrTable>> GetTablesAsync(byte[] documentBytes, Guid fileId, int processRunId)
        {
            CallCount++;
            return Task.FromResult(tables);
        }
    }

    private class ThrowingTableExtractorService : ITableExtractorService
    {
        public string Name => "ThrowingTableExtractorService";

        public Task<IReadOnlyList<OcrTable>> GetTablesAsync(byte[] documentBytes, Guid fileId, int processRunId) =>
            throw new InvalidOperationException("Simulated local-parser failure on a malformed PDF");
    }

    private static readonly OcrTable FullGridTable = new()
    {
        RowCount = 5,
        ColumnCount = 3,
        Cells =
        [
            new OcrTableCell { RowIndex = 0, ColumnIndex = 0, Content = "Source of supply: ✓" },
            new OcrTableCell { RowIndex = 0, ColumnIndex = 1, Content = "Quantities: ✓" },
            new OcrTableCell { RowIndex = 0, ColumnIndex = 2, Content = "Land (only if specified): N/A" },
            new OcrTableCell { RowIndex = 1, ColumnIndex = 0, Content = "Point of abstraction: ✓" },
            new OcrTableCell { RowIndex = 1, ColumnIndex = 1, Content = "Means of measurement: ✓" },
            new OcrTableCell { RowIndex = 1, ColumnIndex = 2, Content = "Charging factors: N/A" },
            new OcrTableCell { RowIndex = 2, ColumnIndex = 0, Content = "Means of abstraction: ✓" },
            new OcrTableCell { RowIndex = 2, ColumnIndex = 1, Content = "Records: ✓" },
            new OcrTableCell { RowIndex = 2, ColumnIndex = 2, Content = "Other provisions (specify below): N/A" },
            new OcrTableCell { RowIndex = 3, ColumnIndex = 0, Content = "Purpose(s): ✓" },
            new OcrTableCell { RowIndex = 3, ColumnIndex = 1, Content = "Provision of information: ✓" },
            new OcrTableCell { RowIndex = 4, ColumnIndex = 0, Content = "Period: ✓" },
            new OcrTableCell { RowIndex = 4, ColumnIndex = 1, Content = "Special conditions: N/A" }
        ]
    };

    private static LabelGroupResult HeuristicResult(string labelGroupName, string text) => new()
    {
        LabelGroupName = labelGroupName,
        MatchedLabelName = labelGroupName,
        ServiceName = "PdfPigNoOcr",
        Text = [new DocumentLine { Columns = [new DocumentLineColumn(DocumentLineColumn.TextToWords(text, null))] }]
    };

    [Fact]
    public async Task WhenTableResolvesAField_ThenItReplacesTheHeuristicResultForThatFieldOnly()
    {
        var item = new MatchesResult
        {
            Matches =
            [
                HeuristicResult(WrInspectionReportFieldNames.SpecialConditions, "X"), // Heuristic got this wrong (real trace: fabricated verdict)
                HeuristicResult(WrInspectionReportFieldNames.LicenceNumber, "1/2/3/S/45"), // Unrelated field - must survive untouched
                HeuristicResult(WrInspectionReportFieldNames.SourceOfSupply, "In") // Grid field the table WON'T resolve - must also survive untouched
            ]
        };

        var gridTable = new OcrTable
        {
            RowCount = 5,
            ColumnCount = 2,
            Cells =
            [
                new OcrTableCell { RowIndex = 0, ColumnIndex = 0, Content = "Point of abstraction: ✓" },
                new OcrTableCell { RowIndex = 0, ColumnIndex = 1, Content = "Means of measurement: ✓" },
                new OcrTableCell { RowIndex = 1, ColumnIndex = 0, Content = "Means of abstraction: ✓" },
                new OcrTableCell { RowIndex = 1, ColumnIndex = 1, Content = "Records: ✓" },
                new OcrTableCell { RowIndex = 2, ColumnIndex = 0, Content = "Purpose(s): ✓" },
                new OcrTableCell { RowIndex = 2, ColumnIndex = 1, Content = "Provision of information: ✓" },
                new OcrTableCell { RowIndex = 3, ColumnIndex = 0, Content = "Period: ✓" },
                // The field the table correctly resolves, overriding a wrong heuristic verdict.
                new OcrTableCell { RowIndex = 3, ColumnIndex = 1, Content = "Special conditions: N/A" },
                new OcrTableCell { RowIndex = 4, ColumnIndex = 0, Content = "Quantities: ✓" },
                new OcrTableCell { RowIndex = 4, ColumnIndex = 1, Content = "Charging factors: N/A" }
                // Deliberately no "Source of supply" cell at all - table can't resolve it.
            ]
        };

        var tableExtractorService = new FakeTableExtractorService([gridTable]);

        await WrInspectionReportExtractionOrchestrator.ApplyTableBasedGridMatchesAsync(
            item, Labels, tableExtractorService, [1, 2, 3], Guid.NewGuid(), processRunId: 1);

        var specialConditions = item.Matches!.Single(m => m.LabelGroupName == WrInspectionReportFieldNames.SpecialConditions);
        Assert.Equal("N/A", specialConditions.Text!.Single().Text);
        Assert.Equal("FakeTableExtractorService", specialConditions.ServiceName);

        var licenceNumber = item.Matches!.Single(m => m.LabelGroupName == WrInspectionReportFieldNames.LicenceNumber);
        Assert.Equal("1/2/3/S/45", licenceNumber.Text!.Single().Text);
        Assert.Equal("PdfPigNoOcr", licenceNumber.ServiceName);

        var sourceOfSupply = item.Matches!.Single(m => m.LabelGroupName == WrInspectionReportFieldNames.SourceOfSupply);
        Assert.Equal("In", sourceOfSupply.Text!.Single().Text);
        Assert.Equal("PdfPigNoOcr", sourceOfSupply.ServiceName);

        // Every field originally present that the table DIDN'T touch is still present exactly
        // once - the merge never drops an existing heuristic result it isn't replacing.
        Assert.Single(item.Matches!, m => m.LabelGroupName == WrInspectionReportFieldNames.LicenceNumber);
        Assert.Single(item.Matches!, m => m.LabelGroupName == WrInspectionReportFieldNames.SourceOfSupply);
        Assert.Single(item.Matches!, m => m.LabelGroupName == WrInspectionReportFieldNames.SpecialConditions);
    }

    [Fact]
    public async Task WhenNoTableResemblesTheGrid_ThenAllHeuristicResultsSurviveUnchanged()
    {
        var item = new MatchesResult
        {
            Matches = [HeuristicResult(WrInspectionReportFieldNames.SourceOfSupply, "In")]
        };

        var unrelatedTable = new OcrTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [new OcrTableCell { RowIndex = 0, ColumnIndex = 0, Content = "Meter make: ABB" }]
        };

        var tableExtractorService = new FakeTableExtractorService([unrelatedTable]);

        await WrInspectionReportExtractionOrchestrator.ApplyTableBasedGridMatchesAsync(
            item, Labels, tableExtractorService, [1, 2, 3], Guid.NewGuid(), processRunId: 1);

        var sourceOfSupply = Assert.Single(item.Matches!);
        Assert.Equal("In", sourceOfSupply.Text!.Single().Text);
        Assert.Equal("PdfPigNoOcr", sourceOfSupply.ServiceName);
    }

    // Cost-optimised two-tier design: a free/local primary extractor is tried first, and a
    // paid/cloud fallback is only ever invoked - so only ever billed - when the primary resolved
    // NOTHING confidently. The three tests below lock in both halves of that contract: the
    // fallback firing when it's genuinely needed, and NOT firing (the actual cost saving) when
    // the primary already found something.

    [Fact]
    public async Task WhenPrimaryFindsNothingConfident_ThenFallbackResultsAreUsed()
    {
        var item = new MatchesResult
        {
            Matches = [HeuristicResult(WrInspectionReportFieldNames.SourceOfSupply, "In")]
        };

        var unrelatedTable = new OcrTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [new OcrTableCell { RowIndex = 0, ColumnIndex = 0, Content = "Meter make: ABB" }]
        };

        var primary = new FakeTableExtractorService([unrelatedTable]);
        var fallback = new FakeTableExtractorService([FullGridTable]);

        await WrInspectionReportExtractionOrchestrator.ApplyTableBasedGridMatchesAsync(
            item, Labels, primary, [1, 2, 3], Guid.NewGuid(), processRunId: 1, fallback);

        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, fallback.CallCount);

        var sourceOfSupply = item.Matches!.Single(m => m.LabelGroupName == WrInspectionReportFieldNames.SourceOfSupply);
        Assert.Equal("✓", sourceOfSupply.Text!.Single().Text);
        Assert.Equal("FakeTableExtractorService", sourceOfSupply.ServiceName);
    }

    [Fact]
    public async Task WhenPrimaryResolvesAtLeastOneField_ThenFallbackIsNeverCalled()
    {
        var item = new MatchesResult
        {
            Matches = [HeuristicResult(WrInspectionReportFieldNames.SourceOfSupply, "Not")]
        };

        var primary = new FakeTableExtractorService([FullGridTable]);
        var fallback = new FakeTableExtractorService([FullGridTable]);

        await WrInspectionReportExtractionOrchestrator.ApplyTableBasedGridMatchesAsync(
            item, Labels, primary, [1, 2, 3], Guid.NewGuid(), processRunId: 1, fallback);

        Assert.Equal(1, primary.CallCount);
        // The actual cost-saving property: a confident primary result must never trigger the
        // paid fallback, even though one was supplied.
        Assert.Equal(0, fallback.CallCount);

        var sourceOfSupply = item.Matches!.Single(m => m.LabelGroupName == WrInspectionReportFieldNames.SourceOfSupply);
        Assert.Equal("✓", sourceOfSupply.Text!.Single().Text);
    }

    [Fact]
    public async Task WhenPrimaryThrows_ThenFallbackIsStillTried()
    {
        var item = new MatchesResult
        {
            Matches = [HeuristicResult(WrInspectionReportFieldNames.SourceOfSupply, "In")]
        };

        var primary = new ThrowingTableExtractorService();
        var fallback = new FakeTableExtractorService([FullGridTable]);

        await WrInspectionReportExtractionOrchestrator.ApplyTableBasedGridMatchesAsync(
            item, Labels, primary, [1, 2, 3], Guid.NewGuid(), processRunId: 1, fallback);

        Assert.Equal(1, fallback.CallCount);

        var sourceOfSupply = item.Matches!.Single(m => m.LabelGroupName == WrInspectionReportFieldNames.SourceOfSupply);
        Assert.Equal("✓", sourceOfSupply.Text!.Single().Text);
        Assert.Equal("FakeTableExtractorService", sourceOfSupply.ServiceName);
    }

    [Fact]
    public async Task WhenBothPrimaryAndFallbackFindNothingConfident_ThenHeuristicResultSurvivesUnchanged()
    {
        var item = new MatchesResult
        {
            Matches = [HeuristicResult(WrInspectionReportFieldNames.SourceOfSupply, "In")]
        };

        var unrelatedTable = new OcrTable
        {
            RowCount = 1,
            ColumnCount = 1,
            Cells = [new OcrTableCell { RowIndex = 0, ColumnIndex = 0, Content = "Meter make: ABB" }]
        };

        var primary = new FakeTableExtractorService([unrelatedTable]);
        var fallback = new FakeTableExtractorService([unrelatedTable]);

        await WrInspectionReportExtractionOrchestrator.ApplyTableBasedGridMatchesAsync(
            item, Labels, primary, [1, 2, 3], Guid.NewGuid(), processRunId: 1, fallback);

        Assert.Equal(1, primary.CallCount);
        Assert.Equal(1, fallback.CallCount);

        var sourceOfSupply = Assert.Single(item.Matches!);
        Assert.Equal("In", sourceOfSupply.Text!.Single().Text);
        Assert.Equal("PdfPigNoOcr", sourceOfSupply.ServiceName);
    }
}
