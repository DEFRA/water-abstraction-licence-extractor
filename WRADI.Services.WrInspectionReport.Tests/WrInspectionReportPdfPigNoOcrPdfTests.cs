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

                    var (stopExecution, _, matchesResult) = await pdfDataExtractor.GetMatchesAsync(
                        fileName,
                        dmsFileData,
                        lookupConfiguration,
                        [fileName],
                        processRunId: -99);

                    if (stopExecution || matchesResult == null)
                    {
                        failures.Add((fileName, "Extraction reported StopExecution or returned no result"));
                        return;
                    }

                    var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, dmsFileData);
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
    }

    private static string Percent(int count, int total) =>
        total == 0 ? "n/a" : $"{count * 100.0 / total:F1}%";
}
