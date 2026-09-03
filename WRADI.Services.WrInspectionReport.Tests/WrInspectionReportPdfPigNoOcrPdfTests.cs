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

        testOutputHelper.WriteLine($"Total files:              {total}");
        testOutputHelper.WriteLine($"Processed without error:  {formsList.Count}");
        testOutputHelper.WriteLine($"Failures:                 {failures.Count}");
        testOutputHelper.WriteLine($"LicenceNumber found:      {licenceNumberFound} ({Percent(licenceNumberFound, total)})");
        testOutputHelper.WriteLine($"InspectionDate found:     {inspectionDateFound} ({Percent(inspectionDateFound, total)})");
        testOutputHelper.WriteLine($"InspectingOfficer found:  {inspectingOfficerFound} ({Percent(inspectingOfficerFound, total)})");
        testOutputHelper.WriteLine($"SourceOfSupply resolved:  {sourceOfSupplyResolved} ({Percent(sourceOfSupplyResolved, total)})");
        testOutputHelper.WriteLine($"SpotCheckResult found:    {spotCheckResultFound} ({Percent(spotCheckResultFound, total)})");
        testOutputHelper.WriteLine($"MeterMake found:          {meterMakeFound} ({Percent(meterMakeFound, total)})");

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
    }

    private static string Percent(int count, int total) =>
        total == 0 ? "n/a" : $"{count * 100.0 / total:F1}%";
}
