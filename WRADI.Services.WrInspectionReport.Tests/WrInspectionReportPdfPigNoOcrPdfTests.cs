using System.Collections.Concurrent;
using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Tabula;
using WALE.ProcessFile.Services.Services;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Models.Csv;
using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.WrInspectionReport.Tests.Config;
using Xunit.Abstractions;

namespace WRADI.Services.WrInspectionReport.Tests;

public class WrInspectionReportPdfPigNoOcrPdfTests(ITestOutputHelper testOutputHelper)
{
    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = new ApiMessageQueueService(new HttpClient());

    private static LookupConfiguration BuildLookupConfiguration(
        string pdfFolder,
        bool textBasedConfig,
        ITableExtractorService tableExtractorService)
    {
        return new LookupConfiguration(
            textBasedConfig
                ? WrInspectionReportTextBasedLabelConfiguration.GetLabels()
                : WrInspectionReportTextBasedLabelConfiguration.GetLabels(),
            [],
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            new NullLicenceNumberService(),
            tableExtractorService,
            tableExtractorService,
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.Now);
    }

    private static IPdfDataExtractorService BuildPdfDataExtractor()
    {
        return new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>
            {
                // No OCR services - WR51 inspection reports are native-text PDFs only
            },
            CacheService,
            OutputService,
            DocumentService,
            DocnetAlternativeDocumentService,
            MessageQueueService);
    }

    /// No hand-verified ground truth for these files, so no exact values are asserted: this proves
    /// the label DSL runs end-to-end without exceptions and reports field coverage.
    [Fact]
    public async Task WhenExtractingRealWr51Documents_ThenNoExceptionsAndReasonableFieldCoverage()
    {
        var pdfFolder = TestConfig.PdfFolder;

        // TestLicences is shared with the licence pipeline's tests, so this filters to WR51 only.
        // The hand-verified "dummy" fixtures belong to Wr51PdfPigNoOcrPdfTests instead.
        var files = Directory.GetFiles(pdfFolder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .Where(f => f.StartsWith("wr51", StringComparison.OrdinalIgnoreCase)
                && !f.Contains("dummy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(files.Count > 0, $"No WR51 PDFs found in {pdfFolder}");

        // The consolidated ruleset reads table free text, so an extractor is required.
        var lookupConfiguration = BuildLookupConfiguration(
            pdfFolder, true, new TabulaTableExtractorService(CacheService));

        var failures = new ConcurrentBag<(string FileName, string Error)>();
        var forms = new ConcurrentBag<global::WRADI.DocumentType.WrInspectionReport.Models.WrInspectionReport>();

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (fileName, _) =>
            {
                var pdfDataExtractor = BuildPdfDataExtractor();

                try
                {
                    var fileId = FileHelper.ExtractFileId(fileName);

                    if (fileId == null)
                    {
                        failures.Add((fileName, "Could not extract file id from filename"));
                        return;
                    }

                    var dmsFileData = new DmsFileData { FileId = fileId.Value };

                    var (stopExecution, _, matchesResult, template) =
                        await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                            fileName,
                            dmsFileData,
                            lookupConfiguration,
                            lookupConfiguration,
                            [fileName],
                            processRunId: -99,
                            pdfDataExtractor);

                    if (stopExecution || matchesResult == null)
                    {
                        failures.Add((fileName, "Extraction reported StopExecution or returned no result"));
                        return;
                    }

                    var form = WrInspectionReportSchemaConverter.ToForm(
                        matchesResult,
                        dmsFileData,
                        template);
                    
                    forms.Add(form);
                }
                catch (Exception ex)
                {
                    failures.Add((fileName, ex.ToString()));
                }
                finally
                {
                    pdfDataExtractor.Dispose();
                }
            });

        var formsList = forms.ToList();
        var total = files.Count;

        Directory.CreateDirectory(OutputService.OutputFolder!);
        var csvPath = Path.Combine(OutputService.OutputFolder!, "_extraction-results.csv");
        var csvRows = formsList
            .OrderBy(f => f.Metadata.Filename, StringComparer.OrdinalIgnoreCase)
            .Select(WrInspectionReportCsvLine.FromForm)
            .ToList();

        await using (var writer = new StreamWriter(csvPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(csvRows);
        }

        testOutputHelper.WriteLine($"CSV written to:           {csvPath}");

        // The per-document CSV above truncates to the first meter, so this is where every later
        // meter on a document appears. Meters are unbounded, hence a file rather than columns.
        var metersPath = Path.Combine(OutputService.OutputFolder!, "_extraction-results-meters.csv");
        var meterRows = formsList
            .OrderBy(f => f.Metadata.Filename, StringComparer.OrdinalIgnoreCase)
            .SelectMany(WrInspectionReportMeterCsvLine.FromForm)
            .ToList();

        await File.WriteAllBytesAsync(
            metersPath,
            WrInspectionReportReportBuilder.BuildCsv(meterRows));

        testOutputHelper.WriteLine($"Meter rows:               {meterRows.Count} from {csvRows.Count} documents");
        testOutputHelper.WriteLine($"Meter CSV written to:     {metersPath}");

        // Zero until v3 documents enter the set: only v3 prints an actions grid. Written
        // regardless so the file is not mistaken for a missing export when it does.
        var actionsPath = Path.Combine(OutputService.OutputFolder!, "_extraction-results-actions.csv");
        var actionRows = formsList
            .OrderBy(f => f.Metadata.Filename, StringComparer.OrdinalIgnoreCase)
            .SelectMany(WrInspectionReportActionCsvLine.FromForm)
            .ToList();

        await File.WriteAllBytesAsync(
            actionsPath,
            WrInspectionReportReportBuilder.BuildCsv(actionRows));

        testOutputHelper.WriteLine(
            $"Action rows:              {actionRows.Count} (v3 only - none in these documents yet)");

        await ReportCoverage(formsList, testOutputHelper, failures, total);
    }

    /// The same documents with the cost-optimised overlay: Tabula first, Azure DI only where
    /// Tabula resolves fewer than minimumFieldsToSkipFallback of the 13 grid fields. Reports
    /// coverage, not accuracy.
    ///
    /// Azure DI is billed on each document's first run here, cached after. Not free to re-run.
    [Fact]
    public async Task WhenExtractingRealWr51DocumentsWithCostOptimizedTableExtraction_ThenReportsCoverageAndFallbackRate()
    {
        if (string.IsNullOrEmpty(TestConfig.AiServicesEndpoint) || string.IsNullOrEmpty(TestConfig.AiServicesKey))
        {
            testOutputHelper.WriteLine(
                "AiServicesEndpoint/AiServicesKey user secrets not set for " +
                "WRADI.Services.WrInspectionReport.Tests - skipping the all-documents table-extraction run. " +
                "Set both via dotnet user-secrets to enable it.");
            return;
        }

        var pdfFolder = TestConfig.PdfFolder;

        var files = Directory.GetFiles(pdfFolder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .Where(f => f.StartsWith("wr51", StringComparison.OrdinalIgnoreCase)
                && !f.Contains("dummy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(files.Count > 0, $"No WR51 PDFs found in {pdfFolder}");

        var primaryTableExtractorService = new WALE.ProcessFile.Services.Tabula.TabulaTableExtractorService(CacheService);
        var lookupConfiguration = BuildLookupConfiguration(pdfFolder, true, primaryTableExtractorService);
        var lookupConfiguration2 = BuildLookupConfiguration(pdfFolder, false, primaryTableExtractorService);

        var failures = new ConcurrentBag<(string FileName, string Error)>();
        var forms = new ConcurrentBag<DocumentType.WrInspectionReport.Models.WrInspectionReport>();
        var t1FileNames = new ConcurrentBag<string>();
        var fallbackUsedFileNames = new ConcurrentBag<string>();

        await Parallel.ForEachAsync(
            files,
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (fileName, _) =>
            {
                var pdfDataExtractor = BuildPdfDataExtractor();
                // A fresh instance per document mirrors a per-message Lambda invocation, and both
                // extractors are stateless.
                var fallbackTableExtractorService = new WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence.AzureAiServicesDocumentIntelligenceTableExtractorService(
                    TestConfig.AiServicesEndpoint!,
                    TestConfig.AiServicesKey!,
                    CacheService);

                try
                {
                    var fileId = FileHelper.ExtractFileId(fileName);

                    if (fileId == null)
                    {
                        failures.Add((fileName, "Could not extract file id from filename"));
                        return;
                    }

                    var dmsFileData = new DmsFileData { FileId = fileId.Value };
                    var pdfBytes = await File.ReadAllBytesAsync(Path.Combine(pdfFolder, fileName));

                    var (stopExecution, _, matchesResult, template) =
                        await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                            fileName,
                            dmsFileData,
                            lookupConfiguration,
                            lookupConfiguration2,
                            [fileName],
                            processRunId: -99,
                            pdfDataExtractor,
                            primaryTableExtractorService,
                            pdfBytes,
                            fallbackTableExtractorService);

                    if (stopExecution || matchesResult == null)
                    {
                        failures.Add((fileName, "Extraction reported StopExecution or returned no result"));
                        return;
                    }

                    if (template == WrTemplateType.T1)
                    {
                        t1FileNames.Add(fileName);

                        if (matchesResult.Matches!.Any(m => m.ServiceName == fallbackTableExtractorService.Name))
                        {
                            fallbackUsedFileNames.Add(fileName);
                        }
                    }

                    var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, dmsFileData, template);
                    forms.Add(form);
                }
                catch (Exception ex)
                {
                    failures.Add((fileName, ex.Message));
                }
                finally
                {
                    pdfDataExtractor.Dispose();
                }
            });

        var formsList = forms.ToList();

        testOutputHelper.WriteLine($"T1 documents:             {t1FileNames.Count}");
        testOutputHelper.WriteLine(
            $"Paid Azure DI fallback used on {fallbackUsedFileNames.Count}/{t1FileNames.Count} T1 documents " +
            $"({(t1FileNames.Count == 0 ? 0 : (double)fallbackUsedFileNames.Count / t1FileNames.Count):P1}) - " +
            $"the rest resolved via Tabula alone, at no API cost.");
        testOutputHelper.WriteLine("");

        Directory.CreateDirectory(OutputService.OutputFolder!);
        var csvPath = Path.Combine(OutputService.OutputFolder!, "_extraction-results-cost-optimized.csv");
        var csvRows = formsList
            .OrderBy(f => f.Metadata.Filename, StringComparer.OrdinalIgnoreCase)
            .Select(WrInspectionReportCsvLine.FromForm)
            .ToList();

        await using (var writer = new StreamWriter(csvPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(csvRows);
        }

        testOutputHelper.WriteLine($"CSV written to:           {csvPath}");

        await ReportCoverage(formsList, testOutputHelper, failures, files.Count);
    }

    private static async Task ReportCoverage(
        List<global::WRADI.DocumentType.WrInspectionReport.Models.WrInspectionReport> formsList,
        ITestOutputHelper testOutputHelper,
        IReadOnlyCollection<(string FileName, string Error)> failures,
        int total)
    {
        var licenceNumberFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.LicenceNumber));
        var inspectionDateFound = formsList.Count(f => f.InspectionDate.DateTime != null);
        var inspectingOfficerFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.InspectingOfficer));

        // Counted separately from "not found": captured-but-unparseable means extraction grabbed
        // the wrong thing, which is a defect rather than an absent value.
        var inspectionDateUnparsed = formsList.Count(f =>
            f.InspectionDate.DateTime == null && !string.IsNullOrWhiteSpace(f.InspectionDate.RawDate));

        // Not gated on the parse failing: the converter's salvage usually digs a date back out, so
        // gating reports zero while the capture stays wrong. The cause is the squeezed-layout
        // fallback anchoring on "Inspecting Officer". Column-side routes are measured dead ends -
        // text bounding 774 -> 685, rejecting the officer's row 794 -> 758, and dropping
        // .WholeLine() turns 10 of Wr51PdfPigNoOcrPdfTests red.
        var inspectionDateLeaksOfficer = formsList.Count(f =>
            !string.IsNullOrWhiteSpace(f.InspectionDate.RawDate)
            && !string.IsNullOrWhiteSpace(f.InspectingOfficer)
            && f.InspectionDate.RawDate.Contains(f.InspectingOfficer, StringComparison.OrdinalIgnoreCase));

        var sourceOfSupplyResolved = formsList.Count(f =>
            f.LicenceProvisions.SourceOfSupply is InOrderStatus.InOrder or InOrderStatus.NotInOrder or InOrderStatus.NotApplicable);
        var spotCheckResultFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.MeasurementDetails.SpotCheckResult));
        var meterMakeFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.MeasurementDetails.Meters?.FirstOrDefault()?.MeterMake));

        // Guards against a neighbouring field's label leaking in as the value.
        var siteAddressLeaksEmailLabel = formsList.Count(f =>
            f.Address.SiteAddress?.TrimEnd().EndsWith("Email:", StringComparison.OrdinalIgnoreCase) == true);

        // The next-line same-column fetch swept "Name and address:" in as a second line of the
        // licence number on 197 of 789 files. A handful of genuine multi-licence values remain.
        var licenceNumberLeaksNameAndAddress = formsList.Count(f =>
            f.LicenceNumber?.Contains("name and address", StringComparison.OrdinalIgnoreCase) == true);

        string[] siblingLeakTerms =
        [
            "Calibration:", "Conformance:", "Flow verification:", "Meter verification:",
            "Maintenance:", "Frequency:", "Spot Check Result", "General comments"
        ];
        var calibrationLeaksSiblingLabel = formsList.Count(f =>
            f.MeasurementDetails.Calibration != null
            && (siblingLeakTerms.Any(t => f.MeasurementDetails.Calibration.Contains(t, StringComparison.OrdinalIgnoreCase))
                || f.MeasurementDetails.Calibration.Contains("Certificate", StringComparison.OrdinalIgnoreCase)));
        var meterVerificationLeaksSiblingLabel = formsList.Count(f =>
            f.MeasurementDetails.MeterVerification != null
            && siblingLeakTerms.Any(t => f.MeasurementDetails.MeterVerification.Contains(t, StringComparison.OrdinalIgnoreCase)));

        // The row below these four fields' shared label row is Maintenance's own on several
        // templates, not their value row, so its "Y:"/"N:" leaked in as the answer.
        var conformanceBareYOrN = formsList.Count(f =>
            f.MeasurementDetails.Conformance is "Y:" or "N:");
        var flowVerificationBareYOrN = formsList.Count(f =>
            f.MeasurementDetails.FlowVerification is "Y:" or "N:");

        // Found by sweeping every text field against every known page label, rather than waiting
        // for one to be reported. MetWith and InspectingOfficer need TruncateAtKnownSiblingLabel
        // instead of a TextEnd bound; see that method for why.
        var inspectionClassLeaksEmailLabel = formsList.Count(f =>
            f.InspectionClass?.Contains("Email", StringComparison.OrdinalIgnoreCase) == true);
        var metWithLeaksPositionLabel = formsList.Count(f =>
            f.MetWith.Name?.Contains("Position:", StringComparison.OrdinalIgnoreCase) == true);
        var inspectingOfficerLeaksInspectionDateLabel = formsList.Count(f =>
            f.InspectingOfficer?.Contains("Inspection Date:", StringComparison.OrdinalIgnoreCase) == true);

        var templateCounts = formsList
            .GroupBy(f => f.Metadata.Template)
            .OrderByDescending(g => g.Count())
            .ToList();

        var templateDistributionRows = templateCounts
            .Select(g => new TemplateDistributionRow(g.Key.ToString(), g.Count(), Percent(g.Count(), total)))
            .ToList();

        var templateDistributionPath = Path.Combine(OutputService.OutputFolder!, "_template-distribution.csv");
        await using (var writer = new StreamWriter(templateDistributionPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(templateDistributionRows);
        }

        var templateFieldCoverageRows = templateCounts
            .SelectMany(g =>
            {
                var groupList = g.ToList();
                var groupTotal = groupList.Count;

                return Wr51GroundTruthAccuracyTests.FieldExtractors.Select(kv =>
                {
                    var nonBlank = groupList.Count(f => !string.IsNullOrWhiteSpace(kv.Value(f)));
                    return new TemplateFieldCoverageRow(g.Key.ToString(), kv.Key, groupTotal, nonBlank, Percent(nonBlank, groupTotal));
                });
            })
            .OrderBy(r => r.Template, StringComparer.OrdinalIgnoreCase)
            .ThenBy(r => r.Field, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var templateCoveragePath = Path.Combine(OutputService.OutputFolder!, "_template-coverage.csv");
        await using (var writer = new StreamWriter(templateCoveragePath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(templateFieldCoverageRows);
        }

        // One row per document for a human spot-check, flagging only fields usually populated for
        // this document's template but blank here.
        var coverageByTemplateField = templateFieldCoverageRows
            .ToDictionary(r => (r.Template, r.Field), r => r.Documents == 0 ? 0.0 : r.NonBlank / (double)r.Documents);

        var licenceProvisionsFields = Wr51GroundTruthAccuracyTests.FieldExtractors.Keys
            .Where(k => k.StartsWith("LicenceProvisions.", StringComparison.Ordinal))
            .ToList();
        var measurementDetailsFields = Wr51GroundTruthAccuracyTests.FieldExtractors.Keys
            .Where(k => k.StartsWith("MeasurementDetails.", StringComparison.Ordinal))
            .ToList();

        // Only worth flagging if most documents of the template have it, or a sparse field flags
        // on every one.
        const double usuallyPopulatedThreshold = 0.6;

        var qaReviewRows = formsList
            .Select(f =>
            {
                var templateName = f.Metadata.Template.ToString();

                var flaggedFields = Wr51GroundTruthAccuracyTests.FieldExtractors
                    .Where(kv =>
                        string.IsNullOrWhiteSpace(kv.Value(f))
                        && coverageByTemplateField.TryGetValue((templateName, kv.Key), out var coverage)
                        && coverage >= usuallyPopulatedThreshold)
                    .Select(kv => kv.Key)
                    .OrderBy(k => k, StringComparer.OrdinalIgnoreCase)
                    .ToList();

                return new QaReviewRow(
                    f.Metadata.Filename ?? "",
                    templateName,
                    ReviewPriority(f.Metadata.Template),
                    f.LicenceNumber,
                    Wr51GroundTruthAccuracyTests.FieldExtractors["InspectionDate"](f),
                    f.Address.NameAndAddress,
                    f.InspectingOfficer,
                    licenceProvisionsFields.Count(k => !string.IsNullOrWhiteSpace(Wr51GroundTruthAccuracyTests.FieldExtractors[k](f))),
                    licenceProvisionsFields.Count,
                    measurementDetailsFields.Count(k => !string.IsNullOrWhiteSpace(Wr51GroundTruthAccuracyTests.FieldExtractors[k](f))),
                    measurementDetailsFields.Count,
                    flaggedFields.Count,
                    string.Join("; ", flaggedFields),
                    ReviewedBy: "",
                    ReviewOutcome: "",
                    ReviewNotes: "");
            })
            .OrderBy(r => PriorityRank(r.ReviewPriority))
            .ThenByDescending(r => r.FlaggedFieldCount)
            .ThenBy(r => r.Filename, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var qaReviewPath = Path.Combine(OutputService.OutputFolder!, "_qa-review.csv");
        await using (var writer = new StreamWriter(qaReviewPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(qaReviewRows);
        }

        testOutputHelper.WriteLine($"Total files:              {total}");
        testOutputHelper.WriteLine($"Processed without error:  {formsList.Count}");
        testOutputHelper.WriteLine($"Failures:                 {failures.Count}");
        testOutputHelper.WriteLine($"LicenceNumber found:      {licenceNumberFound} ({Percent(licenceNumberFound, total)})");
        testOutputHelper.WriteLine($"InspectionDate found:     {inspectionDateFound} ({Percent(inspectionDateFound, total)})");
        testOutputHelper.WriteLine($"InspectionDate unparsed:  {inspectionDateUnparsed} ({Percent(inspectionDateUnparsed, total)}) - captured text that wouldn't parse");
        testOutputHelper.WriteLine($"InspectionDate col leak:  {inspectionDateLeaksOfficer} ({Percent(inspectionDateLeaksOfficer, total)}) - capture contains the InspectingOfficer value");
        testOutputHelper.WriteLine($"InspectingOfficer found:  {inspectingOfficerFound} ({Percent(inspectingOfficerFound, total)})");
        testOutputHelper.WriteLine($"SourceOfSupply resolved:  {sourceOfSupplyResolved} ({Percent(sourceOfSupplyResolved, total)})");
        testOutputHelper.WriteLine($"SpotCheckResult found:    {spotCheckResultFound} ({Percent(spotCheckResultFound, total)})");
        testOutputHelper.WriteLine($"MeterMake found:          {meterMakeFound} ({Percent(meterMakeFound, total)})");
        testOutputHelper.WriteLine($"SiteAddress 'Email:' leak: {siteAddressLeaksEmailLabel} ({Percent(siteAddressLeaksEmailLabel, total)})");
        testOutputHelper.WriteLine($"LicenceNumber 'Name and address' leak: {licenceNumberLeaksNameAndAddress} ({Percent(licenceNumberLeaksNameAndAddress, total)})");
        testOutputHelper.WriteLine($"Calibration sibling leak: {calibrationLeaksSiblingLabel} ({Percent(calibrationLeaksSiblingLabel, total)})");
        testOutputHelper.WriteLine($"MeterVerif. sibling leak: {meterVerificationLeaksSiblingLabel} ({Percent(meterVerificationLeaksSiblingLabel, total)})");
        testOutputHelper.WriteLine($"Conformance bare 'Y:'/'N:' leak: {conformanceBareYOrN} ({Percent(conformanceBareYOrN, total)})");
        testOutputHelper.WriteLine($"FlowVerification bare 'Y:'/'N:' leak: {flowVerificationBareYOrN} ({Percent(flowVerificationBareYOrN, total)})");
        testOutputHelper.WriteLine($"InspectionClass 'Email' leak: {inspectionClassLeaksEmailLabel} ({Percent(inspectionClassLeaksEmailLabel, total)})");
        testOutputHelper.WriteLine($"MetWith 'Position:' leak: {metWithLeaksPositionLabel} ({Percent(metWithLeaksPositionLabel, total)})");
        testOutputHelper.WriteLine($"InspectingOfficer 'Inspection Date:' leak: {inspectingOfficerLeaksInspectionDateLabel} ({Percent(inspectingOfficerLeaksInspectionDateLabel, total)})");
        testOutputHelper.WriteLine("");
        testOutputHelper.WriteLine("Metadata.Template distribution:");
        foreach (var group in templateCounts)
        {
            testOutputHelper.WriteLine($"  {group.Key,-12} {group.Count()} ({Percent(group.Count(), total)})");
        }
        testOutputHelper.WriteLine($"Template distribution CSV: {templateDistributionPath}");
        testOutputHelper.WriteLine($"Template coverage CSV:    {templateCoveragePath}");
        testOutputHelper.WriteLine($"QA review CSV:            {qaReviewPath}");
        testOutputHelper.WriteLine($"  High priority:          {qaReviewRows.Count(r => r.ReviewPriority == "High")}");
        testOutputHelper.WriteLine($"  Medium priority:        {qaReviewRows.Count(r => r.ReviewPriority == "Medium")}");
        testOutputHelper.WriteLine($"  Normal priority:        {qaReviewRows.Count(r => r.ReviewPriority == "Normal")}");
        testOutputHelper.WriteLine($"  Docs with flagged fields: {qaReviewRows.Count(r => r.FlaggedFieldCount > 0)}");

        if (failures.Count > 0)
        {
            testOutputHelper.WriteLine("");
            testOutputHelper.WriteLine("Failures:");

            foreach (var (fileName, error) in failures.OrderBy(f => f.FileName).Take(50))
            {
                testOutputHelper.WriteLine($"  {fileName}: {error}");
            }
        }

        Assert.True(
            failures.Count == 0,
            $"{failures.Count} of {total} files threw during extraction/conversion - see test output for details");

        Assert.True(
            licenceNumberFound / (double)total >= 0.8,
            $"LicenceNumber was only found on {Percent(licenceNumberFound, total)} of files, expected >= 80%");

        Assert.True(
            siteAddressLeaksEmailLabel == 0,
            $"{siteAddressLeaksEmailLabel} SiteAddress values end with a leaked 'Email:' label - " +
            "the additionalSameLineEndTexts fix on SiteAddress regressed");

        Assert.True(
            calibrationLeaksSiblingLabel == 0,
            $"{calibrationLeaksSiblingLabel} Calibration values contain a leaked sibling-field label " +
            "or the 'Calibration Certificate' collision - the IgnoreBlockIfContains fix regressed");

        // A ratchet, not a target: the one remaining document has a genuinely blank date field.
        // Lower it whenever the gap is reduced.
        const int knownUnparsedInspectionDates = 1;

        Assert.True(
            inspectionDateUnparsed <= knownUnparsedInspectionDates,
            $"{inspectionDateUnparsed} InspectionDate values captured text that wouldn't parse, up from the "
            + $"known {knownUnparsedInspectionDates}. Extraction is claiming the wrong text, not just missing "
            + "a value - see test output.");

        // These documents do resolve a date, so a capture regression would hide behind the salvage.
        // They are RemoveSiblingValue's guard, not real leaks: InspectingOfficer is itself "2019"
        // or "Time: 10:50" there, so stripping it by value would delete the date's own year.
        const int knownInspectionDateColumnLeaks = 5;

        Assert.True(
            inspectionDateLeaksOfficer <= knownInspectionDateColumnLeaks,
            $"{inspectionDateLeaksOfficer} InspectionDate captures contain the InspectingOfficer value, up "
            + $"from the known {knownInspectionDateColumnLeaks} - the two-column walk is taking more of the "
            + "neighbouring column than it was.");

        Assert.True(
            meterVerificationLeaksSiblingLabel == 0,
            $"{meterVerificationLeaksSiblingLabel} MeterVerification values contain a leaked sibling-field " +
            "label - the IgnoreBlockIfContains fix regressed");

        Assert.True(
            licenceNumberLeaksNameAndAddress == 0,
            $"{licenceNumberLeaksNameAndAddress} LicenceNumber values contain a leaked 'Name and address' " +
            "row - the additionalSameLineEndTexts fix on LicenceNumber regressed");

        Assert.True(
            conformanceBareYOrN == 0,
            $"{conformanceBareYOrN} Conformance values are Maintenance's own leaked 'Y:'/'N:' - the " +
            "ExcludeNextLineIfFirstColumnStartsWith fix regressed");

        Assert.True(
            flowVerificationBareYOrN == 0,
            $"{flowVerificationBareYOrN} FlowVerification values are Maintenance's own leaked 'Y:'/'N:' - the " +
            "ExcludeNextLineIfFirstColumnStartsWith fix regressed");

        Assert.True(
            inspectionClassLeaksEmailLabel == 0,
            $"{inspectionClassLeaksEmailLabel} InspectionClass values contain a leaked 'Email' label - " +
            "the additionalSameLineEndTexts fix regressed");

        Assert.True(
            metWithLeaksPositionLabel == 0,
            $"{metWithLeaksPositionLabel} MetWith values contain a leaked 'Position:' label - " +
            "the TruncateAtKnownSiblingLabel fix regressed");

        Assert.True(
            inspectingOfficerLeaksInspectionDateLabel == 0,
            $"{inspectingOfficerLeaksInspectionDateLabel} InspectingOfficer values contain a leaked " +
            "'Inspection Date:' label - the TruncateAtKnownSiblingLabel fix regressed");
    }

    private static string Percent(int count, int total) =>
        total == 0 ? "n/a" : $"{count * 100.0 / total:F1}%";

    // Categorical rather than a pinned number that goes stale: High is where the classifier is
    // least sure or the golden set is thin, Normal is T1 and T6.
    private static string ReviewPriority(WrTemplateType template) => template switch
    {
        WrTemplateType.Unknown => "High",
        WrTemplateType.T4 or WrTemplateType.T7 or WrTemplateType.Impounding => "High",
        WrTemplateType.NonStandardNarrative => "Medium",
        _ => "Normal"
    };

    private static int PriorityRank(string reviewPriority) => reviewPriority switch
    {
        "High" => 0,
        "Medium" => 1,
        _ => 2
    };

    private record TemplateDistributionRow(string Template, int Documents, string PercentOfDocuments);

    private record QaReviewRow(
        string Filename,
        string Template,
        string ReviewPriority,
        string? LicenceNumber,
        string? InspectionDate,
        string? NameAndAddress,
        string? InspectingOfficer,
        int LicenceProvisionsAnswered,
        int LicenceProvisionsTotal,
        int MeasurementDetailsAnswered,
        int MeasurementDetailsTotal,
        int FlaggedFieldCount,
        string FlaggedFields,
        string ReviewedBy,
        string ReviewOutcome,
        string ReviewNotes);

    // Coverage, not accuracy: only the 46-document golden set has truth. Useful at full scale
    // exactly where that set is thinnest (T4=3, T7=1, Impounding=1 there). One row per template
    // and field, since a fixed-column shape does not scale to ~50 fields across 7 templates.
    private record TemplateFieldCoverageRow(string Template, string Field, int Documents, int NonBlank, string Coverage);
}