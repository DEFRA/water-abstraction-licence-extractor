using System.Collections.Concurrent;
using System.Globalization;
using CsvHelper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Csv;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Services;
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

    private static LookupConfiguration BuildLookupConfiguration(string pdfFolder)
    {
        return new LookupConfiguration(
            WrInspectionReportLabelConfiguration.GetLabels(),
            [],
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            new NullLicenceNumberService(),
            new DmsLookupService(),
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            lineHeight: 6,
            minimumRowsForDigital: 30,
            useAnchoredLineGrouping: true);
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

    /// <summary>
    /// Smoke/coverage test against the real WR51 corpus. There's no hand-verified ground truth
    /// for these files, so this doesn't assert exact field values - it proves the ported label
    /// DSL (including the new LimitTo column-restriction behaviour) runs end-to-end against real
    /// documents without exceptions, and reports basic field-coverage stats.
    /// </summary>
    [Fact]
    public async Task WhenExtractingRealWr51Corpus_ThenNoExceptionsAndReasonableFieldCoverage()
    {
        var pdfFolder = TestConfig.PdfFolder;

        // TestLicences is shared with the licence pipeline's own integration tests, so
        // filter to just the WR51 corpus rather than picking up every licence PDF too.
        // The handful of hand-verified "dummy" fixtures (WR51__<licence>__dummy.pdf, no
        // real DMS GUID in the filename) belong to Wr51PdfPigNoOcrPdfTests instead, which
        // derives a stable id via GuidHelper rather than relying on a real one.
        var files = Directory.GetFiles(pdfFolder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(f => f != null)
            .Select(f => f!)
            .Where(f => f.StartsWith("wr51", StringComparison.OrdinalIgnoreCase)
                && !f.Contains("dummy", StringComparison.OrdinalIgnoreCase))
            .ToList();

        Assert.True(files.Count > 0, $"No WR51 PDFs found in {pdfFolder}");

        var lookupConfiguration = BuildLookupConfiguration(pdfFolder);

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

                    var (stopExecution, _, matchesResult, template) = await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                        fileName,
                        dmsFileData,
                        lookupConfiguration,
                        [fileName],
                        processRunId: -99,
                        pdfDataExtractor);

                    if (stopExecution || matchesResult == null)
                    {
                        failures.Add((fileName, "Extraction reported StopExecution or returned no result"));
                        return;
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

        var licenceNumberFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.LicenceNumber));
        var inspectionDateFound = formsList.Count(f => f.InspectionDate.DateTime != null);
        var inspectingOfficerFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.InspectingOfficer));

        var sourceOfSupplyResolved = formsList.Count(f =>
            f.LicenceProvisions.SourceOfSupply is InOrderStatus.InOrder or InOrderStatus.NotInOrder or InOrderStatus.NotApplicable);
        var spotCheckResultFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.MeasurementDetails.SpotCheckResult));
        var meterMakeFound = formsList.Count(f => !string.IsNullOrWhiteSpace(f.MeasurementDetails.MeterMake));

        // Regression guards for data-quality fixes: a neighbouring field's own label leaking
        // into these values, rather than a real answer.
        var siteAddressLeaksEmailLabel = formsList.Count(f =>
            f.Address.SiteAddress?.TrimEnd().EndsWith("Email:", StringComparison.OrdinalIgnoreCase) == true);

        // The next-line same-column fetch swept the "Name and address:" row in as a second
        // line of the licence number on 197/789 real corpus files - additionalSameLineEndTexts
        // fix. A handful of newline-separated values remain (wrapped labels, genuine
        // multi-licence inspections) - this only guards against the dominant leak returning.
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

        // Was Maintenance's own "Y:"/"N:" sub-fields leaking in as Calibration/Conformance/
        // FlowVerification/MeterVerification's answer - the row immediately below their shared
        // grid label row is consistently Maintenance's own row on several real templates, not a
        // dedicated value row for these four fields. Fixed via
        // LabelToMatch.ExcludeNextLineIfFirstColumnStartsWith(["Maintenance"]) - see analysis doc.
        var conformanceBareYOrN = formsList.Count(f =>
            f.MeasurementDetails.Conformance is "Y:" or "N:");
        var flowVerificationBareYOrN = formsList.Count(f =>
            f.MeasurementDetails.FlowVerification is "Y:" or "N:");

        // Found via a proactive sweep of _extraction-results.csv for sibling field-label text
        // leaking into other fields' values - the same class of bug as the guards above, just
        // discovered by scanning every text field against every known page label rather than
        // waiting for a specific field to be reported. InspectionClass's "Email" leak is fixed
        // via additionalSameLineEndTexts (WalkSameLineColumns' TextEnd bound). MetWith/
        // InspectingOfficer needed a different fix - the same endText approach had zero effect,
        // traced via gated instrumentation to the upstream row/column-grouping having already
        // merged the sibling field's text into the SAME DocumentLineColumn on some documents, so
        // there was no second column for TextEnd to exclude. Fixed instead via
        // TruncateAtKnownSiblingLabel in WrInspectionReportSchemaConverter.cs - a plain string
        // truncation that works regardless of the upstream column structure.
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

        // QA-review export: one row per document for a human spot-check pass, not the raw
        // 51-column dump. Reuses the per-template coverage just computed above to flag only the
        // fields that are usually populated for THIS document's template but came back blank
        // here - a targeted "look at this" list rather than 51 columns to eyeball per row.
        var coverageByTemplateField = templateFieldCoverageRows
            .ToDictionary(r => (r.Template, r.Field), r => r.Documents == 0 ? 0.0 : r.NonBlank / (double)r.Documents);

        var licenceProvisionsFields = Wr51GroundTruthAccuracyTests.FieldExtractors.Keys
            .Where(k => k.StartsWith("LicenceProvisions.", StringComparison.Ordinal))
            .ToList();
        var measurementDetailsFields = Wr51GroundTruthAccuracyTests.FieldExtractors.Keys
            .Where(k => k.StartsWith("MeasurementDetails.", StringComparison.Ordinal))
            .ToList();

        // A field blank on this doc is only worth flagging if most other documents of the same
        // template DO have it - otherwise a genuinely sparse field would flag on every document.
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

    // Categorical, not a pinned accuracy number that would go stale the next time the harness
    // runs - High covers templates the classifier itself is least sure about (Unknown) or where
    // the golden set so far is thin (T4/T7/Impounding all have single-digit sample counts - see
    // analysis/08-wr51-field-report.md section 3), Medium is the heterogeneous narrative bucket,
    // Normal is the two best-covered templates (T1, T6).
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

    private record TemplateDistributionRow(string Template, int Documents, string PercentOfCorpus);

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

    // No ground truth exists for the full real corpus (only the 46-doc golden set has that), so
    // this is coverage - "did the field produce anything" - not accuracy - "was it right". Still
    // useful at full corpus scale precisely where the golden set's per-template samples are too
    // small to trust (T4=3, T7=1, Impounding=1 documents there vs the real counts here). One row
    // per (template, field) rather than one column per field, since it covers every field
    // Wr51GroundTruthAccuracyTests.FieldExtractors knows about (reused from there directly, not
    // duplicated) - a fixed-column shape doesn't scale to ~50 fields across 7 templates.
    private record TemplateFieldCoverageRow(string Template, string Field, int Documents, int NonBlank, string Coverage);
}
