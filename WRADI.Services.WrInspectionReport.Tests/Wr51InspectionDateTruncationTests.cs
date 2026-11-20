using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tabula;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.WrInspectionReport.Tests.Config;
using Xunit.Abstractions;

namespace WRADI.Services.WrInspectionReport.Tests;

/// Values that overrun the next label's column lose their last character, and sometimes an
/// interior one turns into a space. The dates below are what the document says, read with
/// poppler; PdfPig's own page text carries them intact, so the loss happens after extraction.
///
/// Mirrors FileProcessSingleService exactly - the same label set, the same Tabula extractor and
/// the same lineHeight and minimumRowsForDigital. Those layout values drive line grouping, so a
/// configuration built from defaults does not reproduce what the pipeline produced.
public class Wr51InspectionDateTruncationTests(ITestOutputHelper testOutputHelper)
{
    /// Satisfies the config's non-null requirement while contributing no grid, so a run with it
    /// exercises the text path alone.
    private sealed class NoTablesExtractorService : ITableExtractorService
    {
        public Task<IReadOnlyList<DocumentTable>> GetTablesAsync(
            PdfDocument pdfDocument,
            Guid fileId,
            int processRunId) =>
            Task.FromResult<IReadOnlyList<DocumentTable>>([]);

        public string Name => nameof(NoTablesExtractorService);
    }

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");

    public static TheoryData<string, string, string> Cases => new()
    {
        // filename, the inspection date, the time the document shows. Dates are ISO because the
        // assertion is on the parsed value; the documents themselves render them 17.11.2020,
        // 04.08.2016 and 14/11/2023 respectively.
        { "wr51__2673709011r01__03cafb19-9857-1682-db46-7361acb70d1c.pdf", "2020-11-17", "10:00" },
        { "wr51__2673614008__c95f91ac-e34a-f832-408a-2c7b39eba018.pdf", "2016-08-04", "10:00" },
        { "wr51__2839220340__e68f3f54-0d3a-4c61-949e-b831b5eb0597.pdf", "2023-11-14", "09.55" },
    };

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task WhenTheValueOverrunsTheNextColumn_ThenTheWholeDateIsStillExtracted(
        string fileName,
        string expectedDate,
        string expectedTime)
    {
        var withGrid = await ExtractInspectionDateAsync(fileName, useTabula: true);
        var noGrid = await ExtractInspectionDateAsync(fileName, useTabula: false);

        testOutputHelper.WriteLine($"{fileName}");
        testOutputHelper.WriteLine($"  TEMPLATE       {withGrid.MatchedText?.Split(']')[0]}]");
        testOutputHelper.WriteLine($"  document says  date='{expectedDate}' time='{expectedTime}'");
        testOutputHelper.WriteLine($"  with Tabula    date='{withGrid.Date}' time='{withGrid.RawTime}' matcher='{withGrid.MatchedText}'");
        testOutputHelper.WriteLine($"  no grid        date='{noGrid.Date}' time='{noGrid.RawTime}' matcher='{noGrid.MatchedText}'");

        var extracted = withGrid;

        Assert.Equal(expectedDate, extracted.Date);
        Assert.Equal(expectedTime, extracted.RawTime);
    }

    /// Observation only: prints every cell Tabula produces, so where the lost letters went can be
    /// seen rather than inferred.
    [Fact]
    public async Task DumpTabulaCells()
    {
        const string fileName = "wr51__2673709011r01__03cafb19-9857-1682-db46-7361acb70d1c.pdf";
        var path = Path.Combine(TestConfig.PdfFolder, fileName);
        Assert.True(File.Exists(path), $"{fileName} not found");

        var pdfBytes = await File.ReadAllBytesAsync(path);
        var fileId = FileHelper.ExtractFileId(fileName)!.Value;

        var configuration = new LookupConfiguration(
            [],
            [],
            new LocalFileService(TestConfig.PdfFolder),
            CacheService,
            OutputService,
            new NullLicenceNumberService(),
            null!,
            null!,
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.UtcNow,
            lineHeight: 6,
            minimumRowsForDigital: 30);

        var tables = await new TabulaTableExtractorService(CacheService).GetTablesAsync(
            new PdfDocument(
                fileName,
                fileId,
                false,
                pdfBytes,
                pdfBytes.Length,
                OutputService,
                new PdfPigNoOcrPdfDocumentService(),
                new DocnetNoOcrAlternativePdfDocumentService(),
                configuration),
            fileId,
            processRunId: -99);

        testOutputHelper.WriteLine($"tables: {tables.Count}");

        foreach (var table in tables)
        {
            testOutputHelper.WriteLine(
                $"=== page {table.PageNumber} rows={table.RowCount} cols={table.ColumnCount} "
                + $"cells={table.Cells.Count}");

            foreach (var cell in table.Cells.Where(c => !string.IsNullOrWhiteSpace(c.Content)))
            {
                var content = cell.Content!.Replace("\n", "\\n");

                // Only the neighbourhood of the date, to keep the output readable.
                if (content.Contains("17.1") || content.Contains(".202") || content.Contains("Date")
                    || content.Contains("Time") || content.Contains("10:0") || content.Contains("Denye"))
                {
                    testOutputHelper.WriteLine(
                        $"  r{cell.RowIndex} c{cell.ColumnIndex} left={cell.Left:F2} top={cell.Top:F2} "
                        + $"content='{content}'");
                }
            }
        }
    }

    private static async Task<(string? Date, string? RawTime, string? MatchedText)> ExtractInspectionDateAsync(
        string fileName,
        bool useTabula)
    {
        var pdfFolder = TestConfig.PdfFolder;
        var path = Path.Combine(pdfFolder, fileName);

        Assert.True(File.Exists(path), $"{fileName} not found in {pdfFolder}");

        ITableExtractorService tableExtractorService = useTabula
            ? new TabulaTableExtractorService(CacheService)
            : new NoTablesExtractorService();

        // Mirrors FileProcessSingleService exactly, lineHeight and minimumRowsForDigital
        // included: those drive line grouping, so defaults do not reproduce the pipeline.
        var lookupConfiguration = new LookupConfiguration(
            WrInspectionReportTextBasedLabelConfiguration.GetLabels(),
            [],
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            new NullLicenceNumberService(),
            tableExtractorService,
            tableExtractorService,
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            lineHeight: 6,
            minimumRowsForDigital: 30);

        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            // No OCR services - WR51 inspection reports are native-text PDFs only
            new List<IOcrDataExtractorService>(),
            CacheService,
            OutputService,
            new PdfPigNoOcrPdfDocumentService(),
            new DocnetNoOcrAlternativePdfDocumentService(),
            new ApiMessageQueueService(new HttpClient()));

        var fileId = FileHelper.ExtractFileId(fileName);
        Assert.NotNull(fileId);

        var dmsFileData = new DmsFileData { FileId = fileId!.Value };

        var pdfBytes = await File.ReadAllBytesAsync(path);

        var (stopExecution, _, matchesResult, template) =
            await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                fileName,
                dmsFileData,
                lookupConfiguration,
                lookupConfiguration,
                [fileName],
                processRunId: -99,
                pdfDataExtractor,
                // Mirrors FileProcessSingleService: the grid overlay on, the paid fallback off.
                useTabula ? tableExtractorService : null,
                useTabula ? pdfBytes : null);

        Assert.False(stopExecution, $"{fileName} reported StopExecution");
        Assert.NotNull(matchesResult);

        var form = WrInspectionReportSchemaConverter.ToForm(matchesResult!, dmsFileData, template);

        // What the matcher captured, before the converter parses it - locates the loss.
        var matched = $"[template={template}] "
            + FlattenMatchedText(matchesResult!, WrInspectionReportFieldNames.InspectionDate)
            + "  ||  Time group: "
            + (FlattenMatchedText(matchesResult!, WrInspectionReportFieldNames.Time) ?? "<no match>");

        // The parsed date, not RawDate: a rule that spans a wrapped layout captures surrounding
        // text by design, so the raw value is an intermediate rather than the result.
        return (form.InspectionDate.DateTime?.ToString("yyyy-MM-dd"), form.InspectionDate.RawTime, matched);
    }

    private static string? FlattenMatchedText(MatchesResult matchesResult, string labelName)
    {
        var group = matchesResult.Matches?.FirstOrDefault(m =>
            string.Equals(m.MatchedLabelName, labelName, StringComparison.OrdinalIgnoreCase));

        if (group?.Text == null)
        {
            return null;
        }

        return string.Join(
            " | ",
            group.Text.Select(line => string.Join(
                " / ",
                line.Columns.Select(column => column.Text))));
    }
}
