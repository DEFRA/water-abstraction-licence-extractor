using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tabula;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Constants;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Models.Csv;
using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.WrInspectionReport.Tests.Helper;
using Xunit.Abstractions;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// WRADI-418. Measures the v3 "Assessment report" ruleset against the six consultation documents
/// in ~/Documents/TestLicences/wr51-t3, and checks that a v3 document classifies as
/// <see cref="WrTemplateType.V3"/> rather than falling through to Unknown.
///
/// Coverage, not accuracy: there is no ground truth for these yet. The real test files for phase
/// 1.1 (20 standard plus 5 with amended tables) do not exist, so this says which fields the rules
/// find, not whether the values are right. Treat a 100% here as "the label matched", nothing more.
///
/// The documents are consultation drafts, so unfilled Word dropdowns read as the literal text
/// "Choose an item." - a placeholder rather than a value. The test counts those separately so they
/// cannot flatter the coverage figure.
///
/// Skips with a message when the folder is absent, so an ordinary test run is unaffected.
///
/// WhenV3ConsultationDocuments_ThenWriteCsv is the one test here that produces a deliverable
/// rather than a measurement.
/// </summary>
public class WrInspectionV3TemplateTests(ITestOutputHelper testOutputHelper)
{
    private static string V3Folder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents",
        "TestLicences",
        "wr51-t3");

    private const string DropdownPlaceholder = "Choose an item.";

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();

    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();

    private static readonly IMessageQueueService MessageQueueService =
        new ApiMessageQueueService(new HttpClient());

    private static readonly ITableExtractorService UnstructuredTableExtractorService =
        new TabulaTableExtractorService(CacheService);

    /// <summary>
    /// The fields a v3 document should always carry. The section blocks are deliberately excluded:
    /// two of the three tables are empty in every consultation sample, so requiring them would be
    /// asserting against a draft artefact.
    /// </summary>
    private static readonly string[] AlwaysExpectedFields =
    [
        WrInspectionReportFieldNames.V3DocumentTemplateVersion,
        WrInspectionReportFieldNames.V3LicenceNumber,
        WrInspectionReportFieldNames.V3LicenceCriticality,
        WrInspectionReportFieldNames.V3LicenceHolderName,
        WrInspectionReportFieldNames.V3LicenceHolderAddress,
        WrInspectionReportFieldNames.V3SiteContactNumber,
        WrInspectionReportFieldNames.V3SiteContactEmail,
        WrInspectionReportFieldNames.V3AssessmentType,
        WrInspectionReportFieldNames.V3PersonMetWith,
        WrInspectionReportFieldNames.V3JobTitleOrRole,
        WrInspectionReportFieldNames.V3AssessmentDate,
        WrInspectionReportFieldNames.V3InspectionStartTime,
        WrInspectionReportFieldNames.V3InspectionEndTime,
        WrInspectionReportFieldNames.V3InspectingOfficers,
        WrInspectionReportFieldNames.V3LicenceConditionsBlock,
        WrInspectionReportFieldNames.V3MeterDetailsBlock,
        WrInspectionReportFieldNames.V3GeneralComments
    ];

    private static LookupConfiguration BuildLookupConfiguration(string pdfFolder)
    {
        var config = new LookupConfiguration(
            WrInspectionReportTextBasedLabelConfiguration.GetLabels(),
            [],
            // LocalFileService concatenates the folder and filename directly, so the separator
            // has to be there.
            new LocalFileService(pdfFolder.EndsWith('/') ? pdfFolder : pdfFolder + "/"),
            CacheService,
            OutputService,
            new NullLicenceNumberService(),
            null,
            // FromLetterAndTableGrid needs an unstructured table extractor. Tabula is the
            // free/local one the real-corpus WR51 test uses, so this costs nothing.
            UnstructuredTableExtractorService,
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            useLockExclusivity: false);

        WrInspectionReportLabelConfiguration.ConfigurationPropertiesToSet(config);
        return config;
    }

    private static IPdfDataExtractorService BuildPdfDataExtractor() =>
        new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>(),
            CacheService,
            OutputService,
            DocumentService,
            DocnetAlternativeDocumentService,
            MessageQueueService);

    /// <summary>
    /// Goes through the orchestrator rather than calling the v3 rules directly, so the
    /// classification pass is exercised too. If classification regressed, a v3 document would be
    /// read with v1 rules and the coverage here would collapse, which is the point.
    /// </summary>
    private static async Task<(MatchesResult? Result, WrTemplateType Template)> ExtractAsync(string filename)
    {
        var pdfDataExtractor = BuildPdfDataExtractor();

        try
        {
            var configuration = BuildLookupConfiguration(V3Folder);

            var fileId = GuidHelper.GetConsistentFileIdFromFilename(filename);

            // Through the orchestrator with the table extractor supplied, which is the production
            // path: classification, the v3 ruleset, and the key/value overlay that reads the
            // two-column grids as cells. Passing the bytes is what opts the overlay in.
            var pdfBytes = await File.ReadAllBytesAsync(Path.Combine(V3Folder, filename));

            var (_, _, result, template) = await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                filename,
                new DmsFileData { FileId = fileId },
                configuration,
                BuildLookupConfiguration(V3Folder),
                [filename],
                processRunId: -99,
                pdfDataExtractor,
                UnstructuredTableExtractorService,
                pdfBytes);

            return (result, template);
        }
        finally
        {
            pdfDataExtractor.Dispose();
        }
    }

    private static string? ValueOf(MatchesResult result, string fieldName) =>
        WrInspectionReportSchemaConverter.GetMultilineText(result, fieldName);

    [Fact]
    public async Task WhenV3ConsultationDocuments_ThenClassifiedAsV3AndFieldsAreFound()
    {
        // Arrange
        if (!Directory.Exists(V3Folder))
        {
            testOutputHelper.WriteLine($"Skipped - no v3 documents at {V3Folder}");
            return;
        }

        var files = Directory.GetFiles(V3Folder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            testOutputHelper.WriteLine($"Skipped - no PDFs under {V3Folder}");
            return;
        }

        testOutputHelper.WriteLine($"{files.Count} v3 consultation documents\n");

        var found = AlwaysExpectedFields.ToDictionary(field => field, _ => 0);
        var placeholders = AlwaysExpectedFields.ToDictionary(field => field, _ => 0);
        var classifiedV3 = 0;
        var failures = new List<string>();

        // Act
        foreach (var filename in files)
        {
            var (result, template) = await ExtractAsync(filename);

            if (template == WrTemplateType.V3)
            {
                classifiedV3++;
            }
            else
            {
                failures.Add($"{filename} classified {template}, expected V3");
            }

            if (result == null)
            {
                failures.Add($"{filename} produced no result");
                continue;
            }

            foreach (var field in AlwaysExpectedFields)
            {
                var value = ValueOf(result, field);

                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                found[field]++;

                if (value.Contains(DropdownPlaceholder, StringComparison.OrdinalIgnoreCase))
                {
                    placeholders[field]++;
                }
            }
        }

        // Assert
        testOutputHelper.WriteLine($"Classified as V3: {classifiedV3} of {files.Count}\n");

        // Coverage alone only says a label matched. Printing one document's values makes it
        // possible to see at a glance that a field has not over-captured into its neighbour,
        // which a count can never show.
        var (sample, _) = await ExtractAsync(files[0]);

        if (sample != null)
        {
            testOutputHelper.WriteLine($"Values read from {files[0]}:");

            foreach (var field in AlwaysExpectedFields)
            {
                var value = (ValueOf(sample, field) ?? string.Empty)
                    .ReplaceLineEndings(" / ");

                testOutputHelper.WriteLine(
                    $"  {field,-34} {(value.Length > 90 ? value[..90] + "..." : value)}");
            }

            testOutputHelper.WriteLine("");
        }

        testOutputHelper.WriteLine("Field coverage:");

        foreach (var field in AlwaysExpectedFields)
        {
            var note = placeholders[field] > 0
                ? $"   ({placeholders[field]} unfilled dropdown placeholder)"
                : string.Empty;

            testOutputHelper.WriteLine(
                $"  {field,-34} {found[field]}/{files.Count}{note}");
        }

        if (failures.Count > 0)
        {
            testOutputHelper.WriteLine("");

            foreach (var failure in failures)
            {
                testOutputHelper.WriteLine($"  {failure}");
            }
        }

        Assert.Equal(files.Count, classifiedV3);
        Assert.Empty(failures);

        // Without this a rule change that stops a field matching entirely still passes: an earlier
        // anchor change took GeneralComments from 6/6 to 0/6 and this test reported success.
        var missing = AlwaysExpectedFields
            .Where(field => found[field] < files.Count)
            .Select(field => $"{field} found in only {found[field]} of {files.Count}")
            .ToList();

        Assert.True(missing.Count == 0, string.Join("; ", missing));
    }

    /// <summary>
    /// A v3 document must be refused outright when no table extractor is supplied, rather than
    /// returning a result that looks healthy while its multi-line fields are truncated and its
    /// meters collapsed into one. A silently-degraded result is worse than no result, because
    /// nothing downstream can tell it apart from a good one.
    /// </summary>
    [Fact]
    public async Task WhenNoTableExtractorSupplied_ThenV3ExtractionIsRefused()
    {
        // Arrange
        if (!Directory.Exists(V3Folder))
        {
            testOutputHelper.WriteLine($"Skipped - no v3 documents at {V3Folder}");
            return;
        }

        var filename = Directory.GetFiles(V3Folder, "*.pdf")
            .Select(Path.GetFileName)
            .FirstOrDefault(name => name != null);

        if (filename == null)
        {
            testOutputHelper.WriteLine($"Skipped - no PDFs under {V3Folder}");
            return;
        }

        var pdfDataExtractor = BuildPdfDataExtractor();

        try
        {
            // Act and assert - no table extractor, no bytes
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                WrInspectionReportExtractionOrchestrator.ExtractAsync(
                    filename,
                    new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(filename) },
                    BuildLookupConfiguration(V3Folder),
                    BuildLookupConfiguration(V3Folder),
                    [filename],
                    processRunId: -99,
                    pdfDataExtractor));

            testOutputHelper.WriteLine(exception.Message);

            Assert.Contains("required", exception.Message, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            pdfDataExtractor.Dispose();
        }
    }

    /// <summary>
    /// The point of the key/value overlay for meters: the converter must end up with one record
    /// per meter, not one merged record. The filenames state the borehole count, which is an
    /// external label the rules never see, so it is a fair expectation to assert against.
    /// </summary>
    [Fact]
    public async Task WhenMultipleMeters_ThenConverterProducesOneRecordPerMeter()
    {
        // Arrange
        if (!Directory.Exists(V3Folder))
        {
            testOutputHelper.WriteLine($"Skipped - no v3 documents at {V3Folder}");
            return;
        }

        var expectedByFile = new Dictionary<string, int>
        {
            ["WR51 for consultation v8_metering updatesMUlti meters T1 1 Borehole.pdf"] = 1,
            ["WR51 for consultation v8_metering updatesMUlti meters T1 2 Borehole.pdf"] = 2,
            ["WR51 for consultation v8_metering updatesMUlti meters T1 4 Borehole.pdf"] = 4,
            ["WR51 for consultation v8_metering updatesMUlti meters T1 5 Boreholes.pdf"] = 5
        };

        var failures = new List<string>();

        // Act and assert
        foreach (var (filename, expected) in expectedByFile)
        {
            if (!File.Exists(Path.Combine(V3Folder, filename)))
            {
                testOutputHelper.WriteLine($"  (absent) {filename}");
                continue;
            }

            var (result, _) = await ExtractAsync(filename);
            Assert.NotNull(result);

            var meters = WrInspectionReportSchemaConverter.BuildMetersForTests(result);

            testOutputHelper.WriteLine(
                $"  {meters.Count} meter record(s), expected {expected}  {filename}");

            foreach (var meter in meters)
            {
                testOutputHelper.WriteLine(
                    $"      name={meter.MeterName}  make={meter.MeterMake}  " +
                    $"serial={meter.SerialNumber}  reading={meter.Reading}  units={meter.Units}");
            }

            if (meters.Count != expected)
            {
                failures.Add($"{filename}: {meters.Count} records, expected {expected}");
            }
        }

        Assert.Empty(failures);
    }

    /// <summary>
    /// WRADI-418 feasibility probe, not an assertion about behaviour. The v3 form is mostly
    /// two-column key/value grids, and the two fields the line-based rules truncate
    /// (V3LicenceHolderName, V3LicenceHolderAddress) are both multi-line cells - something a
    /// cell-based read would get right by construction, since DocumentTableCell.Content holds the
    /// whole cell. This dumps what the table extractor actually finds, to say whether that
    /// approach is available at all before anything is built on it.
    /// </summary>
    [Fact]
    public async Task WhenV3Document_ThenReportWhatTheTableExtractorFinds()
    {
        // Arrange
        if (!Directory.Exists(V3Folder))
        {
            testOutputHelper.WriteLine($"Skipped - no v3 documents at {V3Folder}");
            return;
        }

        var filename = Directory.GetFiles(V3Folder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();

        if (filename == null)
        {
            testOutputHelper.WriteLine($"Skipped - no PDFs under {V3Folder}");
            return;
        }

        var fileId = GuidHelper.GetConsistentFileIdFromFilename(filename);

        var pdfDocument = await new PdfPigNoOcrDataExtractorService().GetPdfDocumentAsync(
            filename,
            fileId,
            OutputService,
            CacheService,
            DocumentService,
            DocnetAlternativeDocumentService,
            BuildLookupConfiguration(V3Folder),
            -99);

        Assert.NotNull(pdfDocument);

        // Act
        var tables = await UnstructuredTableExtractorService.GetTablesAsync(pdfDocument, fileId, -99);

        // Assert
        testOutputHelper.WriteLine($"{filename}\n");
        testOutputHelper.WriteLine($"Tables found: {tables.Count}\n");

        var twoColumn = 0;

        foreach (var table in tables.Take(12))
        {
            var isKeyValue = table.ColumnCount == 2;

            if (isKeyValue)
            {
                twoColumn++;
            }

            testOutputHelper.WriteLine(
                $"  page {table.PageNumber}  {table.RowCount}x{table.ColumnCount}" +
                $"  {table.TableType}{(isKeyValue ? "  <- key/value shape" : string.Empty)}");

            foreach (var cell in table.Cells.Where(c => c.RowIndex < 3).OrderBy(c => c.RowIndex).ThenBy(c => c.ColumnIndex))
            {
                var content = (cell.Content ?? string.Empty).ReplaceLineEndings(" | ");

                testOutputHelper.WriteLine(
                    $"      [{cell.RowIndex},{cell.ColumnIndex}] {(content.Length > 70 ? content[..70] + "..." : content)}");
            }
        }

        testOutputHelper.WriteLine($"\nTwo-column tables among those shown: {twoColumn}");
    }

    /// <summary>
    /// The meter block has to hold every meter, not just the first. The sample set is named by
    /// borehole count, so the count in the filename is the expected number of repeats - each one
    /// anchored by its own "Meter location or reference" label.
    /// </summary>
    [Fact]
    public async Task WhenMultipleMeters_ThenEveryMeterBlockIsCaptured()
    {
        // Arrange
        if (!Directory.Exists(V3Folder))
        {
            testOutputHelper.WriteLine($"Skipped - no v3 documents at {V3Folder}");
            return;
        }

        var files = Directory.GetFiles(V3Folder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (files.Count == 0)
        {
            testOutputHelper.WriteLine($"Skipped - no PDFs under {V3Folder}");
            return;
        }

        // Act and assert
        foreach (var filename in files)
        {
            var (result, _) = await ExtractAsync(filename);

            var block = result == null
                ? null
                : ValueOf(result, WrInspectionReportFieldNames.V3MeterDetailsBlock);

            var meterCount = block == null
                ? 0
                : block.Split("Meter location or reference").Length - 1;

            testOutputHelper.WriteLine($"  {meterCount} meter block(s)  {filename}");

            Assert.True(meterCount > 0, $"No meter blocks captured in {filename}");
        }
    }

    /// <summary>
    /// WRADI-418 deliverable: the inspection data from the v3 test documents as a CSV, to be
    /// checked against the forms by hand.
    ///
    /// Writes two files - the full column set, and the one with diagnostic columns dropped, which
    /// is the shape the portal's own export produces. The CSV is one row per document, so it shows
    /// only the first meter; the multi-meter documents are covered by the meter tests above.
    /// </summary>
    [Fact]
    public async Task WhenV3ConsultationDocuments_ThenWriteCsv()
    {
        // Arrange
        if (!Directory.Exists(V3Folder))
        {
            testOutputHelper.WriteLine($"Skipped - no v3 documents at {V3Folder}");
            return;
        }

        var files = Directory.GetFiles(V3Folder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(filename => filename != null)
            .OrderBy(filename => filename)
            .ToList();

        Assert.NotEmpty(files);

        var lines = new List<WrInspectionReportCsvLine>();

        // Act
        foreach (var filename in files)
        {
            var (result, template) = await ExtractAsync(filename!);
            Assert.NotNull(result);

            result.Filename = filename;

            var form = WrInspectionReportSchemaConverter.ToForm(
                result,
                new DmsFileData { FileId = GuidHelper.GetConsistentFileIdFromFilename(filename!) },
                template);

            lines.Add(WrInspectionReportCsvLine.FromForm(form));
        }

        var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        Directory.CreateDirectory(outputFolder);

        var fullPath = Path.Combine(outputFolder, "WR51-v3-all-columns.csv");
        var sharedPath = Path.Combine(outputFolder, "WR51-v3.csv");

        await File.WriteAllBytesAsync(fullPath, WrInspectionReportReportBuilder.BuildCsv(lines));
        await File.WriteAllBytesAsync(
            sharedPath,
            WrInspectionReportReportBuilder.BuildCsv(lines, excludeInternalColumns: true));

        // Assert
        testOutputHelper.WriteLine($"{lines.Count} row(s) written");
        testOutputHelper.WriteLine($"  {fullPath}");
        testOutputHelper.WriteLine($"  {sharedPath}");

        foreach (var line in lines)
        {
            testOutputHelper.WriteLine(string.Empty);
            testOutputHelper.WriteLine($"  {line.Metadata__Filename}");
            testOutputHelper.WriteLine($"      template={line.Metadata__Template}  version={line.Metadata__DocumentTemplateVerison}");
            testOutputHelper.WriteLine($"      licence={line.LicenceNumber}  criticality={line.LicenceCriticality}");
            testOutputHelper.WriteLine($"      class={line.InspectionClass}");
            testOutputHelper.WriteLine($"      holder={Flatten(line.Address__NameAndAddress)}");
            testOutputHelper.WriteLine($"      site={Flatten(line.Address__SiteAddress)}");
            testOutputHelper.WriteLine($"      tel={line.Address__TelephoneNumber}  email={line.Address__Email}");
            testOutputHelper.WriteLine($"      metWith={line.MetWith__Name}  position={line.MetWith__Position}");
            testOutputHelper.WriteLine($"      officer={Flatten(line.InspectingOfficer)}");
            testOutputHelper.WriteLine($"      date={line.InspectionDate__RawDate}  start={line.InspectionDate__RawTime}  end={line.InspectionDate__RawEndTime}");
            testOutputHelper.WriteLine($"      meter1={line.MeasurementDetails__MeterName}  serial={line.MeasurementDetails__SerialNumber}");
        }

        // Every v3 document must reach the shared columns.
        var unmapped = lines
            .Where(line => string.IsNullOrWhiteSpace(line.LicenceNumber)
                || string.IsNullOrWhiteSpace(line.Address__NameAndAddress)
                || string.IsNullOrWhiteSpace(line.InspectionDate__RawDate))
            .Select(line => line.Metadata__Filename)
            .ToList();

        Assert.Empty(unmapped);
    }

    private static string Flatten(string? value) =>
        value == null ? string.Empty : value.ReplaceLineEndings(" / ");
}
