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

/// The v3 "Assessment report" ruleset against the consultation documents in
/// ~/Documents/TestLicences/wr51-t3. Skips with a message when that folder is absent.
///
/// Coverage, not accuracy - there is no ground truth yet, so a 100% here means "the label
/// matched", nothing more. Unfilled Word dropdowns read as the literal "Choose an item." and are
/// counted separately so they cannot flatter the figure.
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

    /// The section blocks are excluded: two of the three tables are empty in every sample, so
    /// requiring them asserts against a draft artefact.
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
            // LocalFileService concatenates folder and filename directly.
            new LocalFileService(pdfFolder.EndsWith('/') ? pdfFolder : pdfFolder + "/"),
            CacheService,
            OutputService,
            new NullLicenceNumberService(),
            null,
            // FromLetterAndTableGrid needs a table extractor; Tabula is free and local.
            UnstructuredTableExtractorService,
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            useLockExclusivity: false);

        WrInspectionReportTextBasedLabelConfiguration.ConfigurationPropertiesToSet(config);
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

    /// Through the orchestrator, not the v3 rules directly, so classification is exercised: if it
    /// regressed, a v3 document would be read with v1 rules and this coverage would collapse.
    private static async Task<(MatchesResult? Result, WrTemplateType Template)> ExtractAsync(string filename)
    {
        var pdfDataExtractor = BuildPdfDataExtractor();

        try
        {
            var configuration = BuildLookupConfiguration(V3Folder);

            var fileId = GuidHelper.GetConsistentFileIdFromFilename(filename);

            // Passing the bytes is what opts the key/value overlay in.
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

    /// The shared names the portal looks a property up by, split by route: the first group is
    /// renamed by WrInspectionV3SharedFieldMapper, the meter group written directly by ApplyMeters.
    /// Both measured present on all 41 documents.
    private static readonly string[] SharedFieldsFromKeyValueCells =
    [
        WrInspectionReportFieldNames.LicenceNumber,
        WrInspectionReportFieldNames.InspectionClass,
        WrInspectionReportFieldNames.InspectionDate,
        WrInspectionReportFieldNames.MetWith,
        WrInspectionReportFieldNames.Position,
        WrInspectionReportFieldNames.TelephoneNumber,
        WrInspectionReportFieldNames.Time,
        WrInspectionReportFieldNames.NameAndAddress,
        WrInspectionReportFieldNames.SiteAddress,
        WrInspectionReportFieldNames.MeterName,
        WrInspectionReportFieldNames.MeterMake,
        WrInspectionReportFieldNames.SerialNumber,
        WrInspectionReportFieldNames.Reading,
        WrInspectionReportFieldNames.Units
    ];

    private static List<DocumentLineWord> WordsOf(MatchesResult result, string fieldName) =>
        (result.Matches ?? [])
            .Where(group => group.MatchedLabelName == fieldName)
            .SelectMany(group => group.Text ?? [])
            .SelectMany(line => line.Columns ?? [])
            .SelectMany(column => column.Words ?? [])
            .ToList();

    /// On the MatchesResult, not through ToForm: the result is saved before anything renders it, so
    /// a check through the converter passes while the stored document carries nothing the portal
    /// can resolve. A missing shared name means the alias pass stopped running before the save; a
    /// present name with no region means a reader is discarding the cell again.
    [Fact]
    public async Task WhenV3Document_ThenSharedFieldsAreStoredWithTheirCellRegions()
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

        var missingField = new List<string>();
        var missingRegion = new List<string>();
        var documents = 0;

        // Act
        foreach (var filename in files)
        {
            var (result, template) = await ExtractAsync(filename);

            if (result == null || template != WrTemplateType.V3)
            {
                continue;
            }

            documents++;

            foreach (var field in SharedFieldsFromKeyValueCells)
            {
                var words = WordsOf(result, field);

                if (words.Count == 0)
                {
                    missingField.Add($"{filename} {field}");
                    continue;
                }

                // Any rather than All: a repeating group holds an absent value as an empty line to
                // keep its position, and that line has no cell behind it to take a region from.
                if (words.All(word => word.Coordinates.Left < 0))
                {
                    missingRegion.Add($"{filename} {field}");
                }
            }
        }

        // Assert
        testOutputHelper.WriteLine($"{documents} v3 documents, "
            + $"{SharedFieldsFromKeyValueCells.Length} shared fields each");

        Assert.NotEqual(0, documents);
        Assert.Empty(missingField);
        Assert.Empty(missingRegion);
    }

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

        // Printing one document's values shows over-capture into a neighbour, which a count cannot.
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

    /// Refused outright with no table extractor: nothing downstream can tell a truncated result
    /// from a good one.
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

    /// One record per meter, not one merged record. The filenames state the borehole count, an
    /// external label the rules never see, so it is fair to assert against.
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

    /// A probe, not an assertion about behaviour: dumps what the table extractor finds, to say
    /// whether a cell-based read of the multi-line holder name and address is available at all.
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

    /// The filename's borehole count is the expected number of repeats, each anchored by its own
    /// "Meter location or reference" label.
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

    /// A deliverable rather than a measurement: the v3 data as CSV, to be checked against the forms
    /// by hand. Three files - the full column set, the portal export's shape with diagnostic
    /// columns dropped, and a per-meter file, since the first two show only the first meter.
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
        var meterLines = new List<WrInspectionReportMeterCsvLine>();
        var actionLines = new List<WrInspectionReportActionCsvLine>();

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
            meterLines.AddRange(WrInspectionReportMeterCsvLine.FromForm(form));
            actionLines.AddRange(WrInspectionReportActionCsvLine.FromForm(form));
        }

        var outputFolder = Path.Combine(Directory.GetCurrentDirectory(), "Output");
        Directory.CreateDirectory(outputFolder);

        var fullPath = Path.Combine(outputFolder, "WR51-v3-all-columns.csv");
        var sharedPath = Path.Combine(outputFolder, "WR51-v3.csv");
        var metersPath = Path.Combine(outputFolder, "WR51-v3-meters.csv");
        var actionsPath = Path.Combine(outputFolder, "WR51-v3-actions.csv");

        await File.WriteAllBytesAsync(fullPath, WrInspectionReportReportBuilder.BuildCsv(lines));
        await File.WriteAllBytesAsync(
            sharedPath,
            WrInspectionReportReportBuilder.BuildCsv(lines, excludeInternalColumns: true));
        await File.WriteAllBytesAsync(
            metersPath,
            WrInspectionReportReportBuilder.BuildCsv(meterLines));
        await File.WriteAllBytesAsync(
            actionsPath,
            WrInspectionReportReportBuilder.BuildCsv(actionLines));

        // Assert
        testOutputHelper.WriteLine(
            $"{lines.Count} document row(s), {meterLines.Count} meter row(s), " +
            $"{actionLines.Count} action row(s)");
        testOutputHelper.WriteLine($"  {fullPath}");
        testOutputHelper.WriteLine($"  {sharedPath}");
        testOutputHelper.WriteLine($"  {metersPath}");
        testOutputHelper.WriteLine($"  {actionsPath}");

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

        // More rows than documents, since the per-document file truncates to the first meter.
        Assert.True(
            meterLines.Count > lines.Count,
            $"Expected more meter rows than documents, got {meterLines.Count} for {lines.Count}");

        foreach (var group in meterLines.GroupBy(line => line.Metadata__Filename))
        {
            Assert.Equal(group.First().MeterCount, group.Count());
        }

        // On the value, not just presence: a regression here reads as a licence being out of order.
        var unresolvedProvisions = lines
            .Where(line => line.LicenceProvisions__SourceOfSupply != nameof(InOrderStatus.InOrder)
                || line.LicenceProvisions__Records == nameof(InOrderStatus.DidntMatch)
                || line.LicenceProvisions__Quantities == nameof(InOrderStatus.DidntMatch))
            .Select(line => line.Metadata__Filename)
            .ToList();

        Assert.Empty(unresolvedProvisions);

        // Only documents with a filled action contribute rows. The text must survive whole: the
        // fragmented copy of this grid splits one action across a row per line.
        Assert.NotEmpty(actionLines);

        foreach (var line in actionLines)
        {
            testOutputHelper.WriteLine(
                $"  action {line.ActionNumber}/{line.ActionCount} type={line.Type} " +
                $"text={line.Action?[..Math.Min(60, line.Action.Length)]}");
        }

        Assert.Contains(
            actionLines,
            line => line.Action != null
                && line.Action.Contains("same day", StringComparison.OrdinalIgnoreCase)
                && line.Action.Contains("record keeping", StringComparison.OrdinalIgnoreCase));
    }

    private static string Flatten(string? value) =>
        value == null ? string.Empty : value.ReplaceLineEndings(" / ");
}
