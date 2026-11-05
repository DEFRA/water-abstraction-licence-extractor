using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;
using WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence;
using WALE.ProcessFile.Services.Cache;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.Output;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using WALE.ProcessFile.Services.Tabula;
using WRADI.DocumentType.WrInspectionReport.Configuration;
using WRADI.DocumentType.WrInspectionReport.Converters;
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Services;
using WRADI.Services.WrInspectionReport.Tests.Config;
using Xunit.Abstractions;
using Form = WRADI.DocumentType.WrInspectionReport.Models.WrInspectionReport;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Ground truth lives OUTSIDE this repo: the PDFs and their <c>.truth.json</c> files hold real
/// names, addresses and phone numbers and must never be committed. Replay-only against the cached
/// PdfPig text.
///
/// With the folder absent - any machine but the one it was labelled on, and CI - this reports and
/// returns rather than failing. A reporting tool, not a gate.
/// </summary>
public class Wr51GroundTruthAccuracyTests(ITestOutputHelper testOutputHelper)
{
    private static readonly string GroundTruthFolder = $"{TestConfig.PdfFolder}/truth";

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = new ApiMessageQueueService(new HttpClient());

    // Two T6-only fields have no property in WrInspectionReportMeasurementDetails at all - a model
    // gap, not an extraction bug, so they report as "Unmodeled" rather than scoring as misses.
    private static readonly HashSet<string> UnmodeledFields =
        ["MeasurementDetails.CalibrationCertificate", "MeasurementDetails.VerificationCertificate"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// The pipeline's own construction, so the scores describe what actually runs.
    private static LookupConfiguration BuildLookupConfiguration(
        string pdfFolder,
        ITableExtractorService tableExtractorService)
    {
        return WrInspectionReportLookupConfigurationFactory.Build(
            new LocalFileService(pdfFolder),
            CacheService,
            OutputService,
            tableExtractorService,
            GeneralConstants.UnsetRegionCode,
            DateTime.Now);
    }

    private static IPdfDataExtractorService BuildPdfDataExtractor()
    {
        return new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>(),
            CacheService,
            OutputService,
            DocumentService,
            DocnetAlternativeDocumentService,
            MessageQueueService);
    }

    private class TruthFile
    {
        public string? SourceFile { get; set; }
        public List<string>? DocumentShape { get; set; }
        public Dictionary<string, TruthField> Fields { get; set; } = new();
    }

    private class TruthField
    {
        public bool Present { get; set; }
        public string? TruthValue { get; set; }
    }

    private enum Outcome
    {
        Unmodeled,
        TrueNegative,
        Hit,
        PartialHit,
        Miss,
        Wrong,
        Hallucination
    }

    private record DetailRow(string SourceFile, string Template, string Field, string Outcome, string? TruthValue, string? ExtractedValue);

    private record TemplateSummaryRow(
        string Template,
        int Documents,
        int TruthPresent,
        int Hit,
        int PartialHit,
        int Miss,
        int Wrong,
        int TruthAbsent,
        int Hallucination,
        string Recall,
        string HallucinationRate);

    private record SummaryRow(
        string Field,
        int TruthPresent,
        int Hit,
        int PartialHit,
        int Miss,
        int Wrong,
        int TruthAbsent,
        int Hallucination,
        int Unmodeled,
        string Recall,
        string HallucinationRate);

    /// Blank, Unknown and DidntMatch mean nothing usable was produced, so they collapse to null -
    /// ToString()-ing them would score each as a confidently extracted wrong value.
    // internal because WrInspectionReportPdfPigNoOcrPdfTests reuses these two.
    internal static string? FormatInOrderStatus(InOrderStatus status) =>
        status is InOrderStatus.Blank or InOrderStatus.Unknown or InOrderStatus.DidntMatch
            ? null
            : status.ToString();

    /// One per ground-truth field key. Where truth holds narrative text instead of a status - some
    /// documents describe provisions in prose - this scores as wrong, which is real signal: the
    /// model has no way to hold a narrative provisions answer, only InOrderStatus.
    internal static readonly Dictionary<string, Func<Form, string?>> FieldExtractors = new()
    {
        ["LicenceNumber"] = f => f.LicenceNumber,
        ["InspectionClass"] = f => f.InspectionClass,
        ["Metadata.DocumentHeader"] = f => f.Metadata.DocumentHeader,
        ["Metadata.DocumentTemplateVersion"] = f => f.Metadata.DocumentTemplateVerison,
        ["Metadata.FormSentTo"] = f => f.Metadata.FormSentTo,
        ["Metadata.Date"] = f => f.Metadata.Date.Date?.ToString("yyyy-MM-dd") ?? f.Metadata.Date.RawDate,
        ["Address.NameAndAddress"] = f => f.Address.NameAndAddress,
        ["Address.TelephoneNumber"] = f => f.Address.TelephoneNumber,
        ["Address.SiteAddress"] = f => f.Address.SiteAddress,
        ["MetWith.Name"] = f => f.MetWith.Name,
        ["MetWith.Position"] = f => f.MetWith.Position,
        ["InspectingOfficer"] = f => f.InspectingOfficer,
        ["InspectionDate"] = f => f.InspectionDate.DateTime?.ToString("yyyy-MM-dd") ?? f.InspectionDate.RawDate,
        ["Time"] = f => f.InspectionDate.RawTime ?? f.InspectionDate.DateTime?.ToString("HH:mm"),
        ["LicenceProvisions.SourceOfSupply"] = f => FormatInOrderStatus(f.LicenceProvisions.SourceOfSupply),
        ["LicenceProvisions.PointOfAbstraction"] = f => FormatInOrderStatus(f.LicenceProvisions.PointOfAbstraction),
        ["LicenceProvisions.MeansOfAbstraction"] = f => FormatInOrderStatus(f.LicenceProvisions.MeansOfAbstraction),
        ["LicenceProvisions.Purposes"] = f => FormatInOrderStatus(f.LicenceProvisions.Purposes),
        ["LicenceProvisions.Period"] = f => FormatInOrderStatus(f.LicenceProvisions.Period),
        ["LicenceProvisions.Quantities"] = f => FormatInOrderStatus(f.LicenceProvisions.Quantities),
        ["LicenceProvisions.MeansOfMeasurement"] = f => FormatInOrderStatus(f.LicenceProvisions.MeansOfMeasurement),
        ["LicenceProvisions.Records"] = f => FormatInOrderStatus(f.LicenceProvisions.Records),
        ["LicenceProvisions.ProvisionOfInformation"] = f => FormatInOrderStatus(f.LicenceProvisions.ProvisionOfInformation),
        ["LicenceProvisions.SpecialConditions"] = f => FormatInOrderStatus(f.LicenceProvisions.SpecialConditions),
        ["LicenceProvisions.Land"] = f => FormatInOrderStatus(f.LicenceProvisions.Land),
        ["LicenceProvisions.ChargingFactors"] = f => FormatInOrderStatus(f.LicenceProvisions.ChargingFactors),
        ["LicenceProvisions.OtherProvisions"] = f => FormatInOrderStatus(f.LicenceProvisions.OtherProvisions),
        ["MeasurementDetails.MeterName"] = f => f.MeasurementDetails.Meters?.FirstOrDefault()?.MeterName,
        ["MeasurementDetails.MeterMake"] = f => f.MeasurementDetails.Meters?.FirstOrDefault()?.MeterMake,
        ["MeasurementDetails.SerialNumber"] = f => f.MeasurementDetails.Meters?.FirstOrDefault()?.SerialNumber,
        ["MeasurementDetails.MeterAssetNumber"] = f => f.MeasurementDetails.Meters?.FirstOrDefault()?.MeterAssetNumber,
        ["MeasurementDetails.Reading"] = f => f.MeasurementDetails.Meters?.FirstOrDefault()?.Reading,
        ["MeasurementDetails.FlowRate"] = f => f.MeasurementDetails.Meters?.FirstOrDefault()?.FlowRate,
        ["MeasurementDetails.Units"] = f => f.MeasurementDetails.Meters?.FirstOrDefault()?.Units,
        ["MeasurementDetails.Other"] = f => f.MeasurementDetails.Other,
        ["MeasurementDetails.CertificatesOrRecordsAvailableFor"] = f => f.MeasurementDetails.CertificatesOrRecordsAvailableFor,
        ["MeasurementDetails.DateOfCertificateOrRecord"] = f =>
            f.MeasurementDetails.DateOfCertificateOrRecord.RawDate
            ?? f.MeasurementDetails.DateOfCertificateOrRecord.Date?.ToString("yyyy-MM-dd"),
        ["MeasurementDetails.Calibration"] = f => f.MeasurementDetails.Calibration,
        ["MeasurementDetails.Conformance"] = f => f.MeasurementDetails.Conformance,
        ["MeasurementDetails.FlowVerification"] = f => f.MeasurementDetails.FlowVerification,
        ["MeasurementDetails.MeterVerification"] = f => f.MeasurementDetails.MeterVerification,
        ["MeasurementDetails.Verification"] = f => f.MeasurementDetails.Verification,
        ["MeasurementDetails.SpotCheckResult"] = f => f.MeasurementDetails.SpotCheckResult,
        ["MeasurementDetails.Maintenance.Maintenance"] = f => f.MeasurementDetails.Maintenance.Maintenance,
        ["MeasurementDetails.Maintenance.Frequency"] = f => f.MeasurementDetails.Maintenance.Frequency,
        ["MeasurementDetails.Maintenance.ByWhom"] = f => f.MeasurementDetails.Maintenance.ByWhom,
        ["MeasurementDetails.ReadingsTaken.ReadingsTaken"] = f => f.MeasurementDetails.ReadingsTaken.ReadingsTaken,
        ["MeasurementDetails.ReadingsTaken.Frequency"] = f => f.MeasurementDetails.ReadingsTaken.Frequency,
        ["MeasurementDetails.ReadingsTaken.ByWhom"] = f => f.MeasurementDetails.ReadingsTaken.ByWhom,
        ["MeasurementDetails.WhereKept"] = f => f.MeasurementDetails.WhereKept,
        ["GeneralComments"] = f => f.GeneralComments
    };

    private static string? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Regex.Replace(value.Trim(), @"\s+", " ").ToLowerInvariant();
    }

    private static string? StripAllWhitespace(string? normalizedValue) =>
        normalizedValue == null ? null : Regex.Replace(normalizedValue, @"\s+", "");

    /// The pipeline joins multi-line values with '\n' where truth was hand-transcribed with ", ",
    /// so whitespace and commas both go, collapsing the two styles to the same characters.
    private static string? StripSeparators(string? normalizedValue) =>
        normalizedValue == null ? null : Regex.Replace(normalizedValue, @"[\s,]+", "");

    // Short structured values score deceptively high: one wrong digit in an 11-digit phone number
    // is already ~91% similar. Every narrative field sits well above this length and every
    // single-value field well below.
    private const int MinLengthForSimilarityScoring = 200;

    // From the 2026-09-08 GeneralComments analysis: encoding near-misses ("m3" vs "³", "13th" vs
    // "13 th") clustered at >=0.90, and every sub-0.90 case was a genuine content difference.
    private const double NearIdenticalSimilarityThreshold = 0.90;

    /// Two-row DP, not a full n*m matrix: GeneralComments runs several thousand characters, where
    /// a full matrix would be tens of megabytes.
    private static double LevenshteinSimilarity(string a, string b)
    {
        if (a.Length == 0 && b.Length == 0)
        {
            return 1.0;
        }

        if (a.Length == 0 || b.Length == 0)
        {
            return 0.0;
        }

        var previousRow = new int[b.Length + 1];
        var currentRow = new int[b.Length + 1];

        for (var j = 0; j <= b.Length; j++)
        {
            previousRow[j] = j;
        }

        for (var i = 1; i <= a.Length; i++)
        {
            currentRow[0] = i;

            for (var j = 1; j <= b.Length; j++)
            {
                var cost = a[i - 1] == b[j - 1] ? 0 : 1;
                currentRow[j] = Math.Min(
                    Math.Min(currentRow[j - 1] + 1, previousRow[j] + 1),
                    previousRow[j - 1] + cost);
            }

            (previousRow, currentRow) = (currentRow, previousRow);
        }

        var distance = previousRow[b.Length];
        return 1.0 - (double)distance / Math.Max(a.Length, b.Length);
    }

    private static Outcome Classify(string fieldName, TruthField truth, string? extractedRaw)
    {
        if (UnmodeledFields.Contains(fieldName))
        {
            return Outcome.Unmodeled;
        }

        var extractedNorm = Normalize(extractedRaw);

        if (!truth.Present)
        {
            return extractedNorm == null ? Outcome.TrueNegative : Outcome.Hallucination;
        }

        var truthNorm = Normalize(truth.TruthValue);

        if (extractedNorm == null)
        {
            return Outcome.Miss;
        }

        // Phone and licence numbers routinely differ only by internal spacing ("01730 813439" vs
        // "01730813439"), which would otherwise report a harness gap as a pipeline bug.
        if (extractedNorm == truthNorm
            || StripAllWhitespace(extractedNorm) == StripAllWhitespace(truthNorm)
            || StripSeparators(extractedNorm) == StripSeparators(truthNorm))
        {
            return Outcome.Hit;
        }

        if (truthNorm != null && (extractedNorm.Contains(truthNorm) || truthNorm.Contains(extractedNorm)))
        {
            return Outcome.PartialHit;
        }

        // Containment above only catches truncation. Narrative fields more often differ by a few
        // characters scattered throughout, neither string containing the other.
        if (truthNorm != null
            && truthNorm.Length >= MinLengthForSimilarityScoring
            && LevenshteinSimilarity(extractedNorm, truthNorm) >= NearIdenticalSimilarityThreshold)
        {
            return Outcome.PartialHit;
        }

        return Outcome.Wrong;
    }

    [Fact]
    public async Task WhenScoringAgainstHandLabelledGoldenSet_ThenReportsPerFieldAccuracy()
    {
        await RunHarnessAsync(tableExtractorService: null, outputSuffix: string.Empty);
    }

    /// Table-based grid extraction via a real Azure Document Intelligence call per document -
    /// about $0.01 a page, cached afterwards. Writes to "-table-based" CSVs so it never clobbers
    /// the baseline above.
    [Fact]
    public async Task WhenScoringWithTableBasedGridExtractionEnabled_ThenReportsPerFieldAccuracy()
    {
        if (string.IsNullOrEmpty(TestConfig.AiServicesEndpoint) || string.IsNullOrEmpty(TestConfig.AiServicesKey))
        {
            testOutputHelper.WriteLine(
                "AiServicesEndpoint/AiServicesKey user secrets not set for " +
                "WRADI.Services.WrInspectionReport.Tests - skipping the table-based comparison run. " +
                "Set both via dotnet user-secrets to enable it.");
            return;
        }

        var tableExtractorService = new AzureAiServicesDocumentIntelligenceTableExtractorService(
            TestConfig.AiServicesEndpoint,
            TestConfig.AiServicesKey,
            CacheService);

        await RunHarnessAsync(tableExtractorService, outputSuffix: "-table-based");
    }

    /// Tabula reads the PDF's own ruling lines, so there is no cloud call, no cost and no secret,
    /// and this always runs. A 4-document spot-check had it competitive with Azure DI.
    [Fact]
    public async Task WhenScoringWithTabulaTableExtractionEnabled_ThenReportsPerFieldAccuracy()
    {
        var tableExtractorService = new TabulaTableExtractorService(CacheService);

        await RunHarnessAsync(tableExtractorService, outputSuffix: "-tabula-table-based");
    }

    /// The two-tier design as wired, at its default minimumFieldsToSkipFallback: Tabula first,
    /// Azure DI billed only where Tabula resolved fewer than that of the 13 grid fields. Also
    /// reports how many T1 documents needed the paid fallback, which is the cost evidence.
    [Fact]
    public async Task WhenScoringWithCostOptimizedTableExtractionEnabled_ThenReportsPerFieldAccuracy()
    {
        if (string.IsNullOrEmpty(TestConfig.AiServicesEndpoint) || string.IsNullOrEmpty(TestConfig.AiServicesKey))
        {
            testOutputHelper.WriteLine(
                "AiServicesEndpoint/AiServicesKey user secrets not set for " +
                "WRADI.Services.WrInspectionReport.Tests - skipping the cost-optimized comparison run. " +
                "Set both via dotnet user-secrets to enable it.");
            return;
        }

        var primaryTableExtractorService = new TabulaTableExtractorService(CacheService);
        var fallbackTableExtractorService = new AzureAiServicesDocumentIntelligenceTableExtractorService(
            TestConfig.AiServicesEndpoint,
            TestConfig.AiServicesKey,
            CacheService);

        await RunHarnessAsync(primaryTableExtractorService, outputSuffix: "-cost-optimized", fallbackTableExtractorService);
    }

    /// Sweeps minimumFieldsToSkipFallback to find where the cost/accuracy curve bends. 1 falls
    /// back only when the primary found nothing; 13 falls back unless Tabula resolved every grid
    /// field. Reuses the cached Azure DI results, so the sweep itself costs nothing.
    [Fact]
    public async Task WhenSweepingTheCostOptimizedFallbackThreshold_ThenReportsTheAccuracyCostCurve()
    {
        if (string.IsNullOrEmpty(TestConfig.AiServicesEndpoint) || string.IsNullOrEmpty(TestConfig.AiServicesKey))
        {
            testOutputHelper.WriteLine(
                "AiServicesEndpoint/AiServicesKey user secrets not set for " +
                "WRADI.Services.WrInspectionReport.Tests - skipping the fallback-threshold sweep. " +
                "Set both via dotnet user-secrets to enable it.");
            return;
        }

        foreach (var threshold in new[] { 1, 4, 7, 10, 13 })
        {
            var primaryTableExtractorService = new TabulaTableExtractorService(CacheService);
            var fallbackTableExtractorService = new AzureAiServicesDocumentIntelligenceTableExtractorService(
                TestConfig.AiServicesEndpoint,
                TestConfig.AiServicesKey,
                CacheService);

            testOutputHelper.WriteLine($"\n=== minimumFieldsToSkipFallback = {threshold} ===");
            await RunHarnessAsync(
                primaryTableExtractorService,
                outputSuffix: $"-cost-optimized-t{threshold}",
                fallbackTableExtractorService,
                threshold);
        }
    }

    /// Satisfies PdfDataExtractorService's non-null requirement without enabling table matching.
    private class EmptyTableExtractorService : ITableExtractorService
    {
        public string Name => "Empty";

        public Task<IReadOnlyList<DocumentTable>> GetTablesAsync(
            PdfDocument pdfDocument, Guid fileId, int processRunId) =>
            Task.FromResult<IReadOnlyList<DocumentTable>>([]);
    }

    private async Task RunHarnessAsync(
        ITableExtractorService? tableExtractorService,
        string outputSuffix,
        ITableExtractorService? fallbackTableExtractorService = null,
        int minimumFieldsToSkipFallback = 10)
    {
        if (!Directory.Exists(GroundTruthFolder))
        {
            testOutputHelper.WriteLine(
                $"Ground-truth folder not found at {GroundTruthFolder} - this is an external, " +
                "non-git-tracked golden set that only exists on the machine it was labelled on. " +
                "Copy the truth files there (a /truth subfolder of this project's PdfFolder " +
                "dotnet user-secret) to run this test. Returning without failure.");
            return;
        }

        var truthPaths = Directory.GetFiles(GroundTruthFolder, "*.truth.json");
        Assert.True(truthPaths.Length > 0, $"No .truth.json files found in {GroundTruthFolder}");

        var pdfFolder = TestConfig.PdfFolder;
        var lookupTableExtractorService = tableExtractorService ?? new EmptyTableExtractorService();
        var lookupConfiguration = BuildLookupConfiguration(pdfFolder, lookupTableExtractorService);

        // The properties cannot express "no tables", so the baseline gets an empty stub. The
        // parameter stays null: coalescing it would turn the baseline into a table-based run.
        var detailRows = new List<DetailRow>();
        var missingPdfs = new List<string>();
        var extractionFailures = new List<(string SourceFile, string Error)>();
        var fallbackUsedDocuments = new List<string>();
        var t1DocumentCount = 0;

        foreach (var truthPath in truthPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var truth = JsonSerializer.Deserialize<TruthFile>(await File.ReadAllTextAsync(truthPath), JsonOptions);

            if (truth?.SourceFile == null)
            {
                extractionFailures.Add((Path.GetFileName(truthPath), "Could not parse truth file or missing sourceFile"));
                continue;
            }

            if (!File.Exists(Path.Combine(pdfFolder, truth.SourceFile)))
            {
                missingPdfs.Add(truth.SourceFile);
                continue;
            }

            var pdfDataExtractor = BuildPdfDataExtractor();

            try
            {
                var fileId = FileHelper.ExtractFileId(truth.SourceFile);

                if (fileId == null)
                {
                    extractionFailures.Add((truth.SourceFile, "Could not extract file id from filename"));
                    continue;
                }

                var dmsFileData = new DmsFileData { FileId = fileId.Value };

                var pdfBytesForTableExtraction = tableExtractorService != null
                    ? await File.ReadAllBytesAsync(Path.Combine(pdfFolder, truth.SourceFile))
                    : null;

                var (stopExecution, _, matchesResult, template) = await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                    truth.SourceFile,
                    dmsFileData,
                    lookupConfiguration,
                    lookupConfiguration,
                    [truth.SourceFile],
                    processRunId: -99,
                    pdfDataExtractor,
                    tableExtractorService,
                    pdfBytesForTableExtraction,
                    fallbackTableExtractorService,
                    minimumFieldsToSkipFallback);

                if (stopExecution || matchesResult == null)
                {
                    extractionFailures.Add((truth.SourceFile, "Extraction reported StopExecution or returned no result"));
                    continue;
                }

                if (template == WrTemplateType.T1)
                {
                    t1DocumentCount++;

                    if (fallbackTableExtractorService != null
                        && matchesResult.Matches!.Any(m => m.ServiceName == fallbackTableExtractorService.Name))
                    {
                        fallbackUsedDocuments.Add(truth.SourceFile);
                    }
                }

                var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, dmsFileData, template);
                var templateName = template.ToString();

                foreach (var (fieldName, truthField) in truth.Fields)
                {
                    string? extractedRaw = null;

                    if (!UnmodeledFields.Contains(fieldName) && !FieldExtractors.TryGetValue(fieldName, out _))
                    {
                        // Schema drift between the golden set and this test, surfaced not skipped.
                        extractionFailures.Add((truth.SourceFile, $"No extractor registered for ground-truth field '{fieldName}'"));
                        continue;
                    }

                    if (!UnmodeledFields.Contains(fieldName))
                    {
                        extractedRaw = FieldExtractors[fieldName](form);
                    }

                    var outcome = Classify(fieldName, truthField, extractedRaw);

                    detailRows.Add(new DetailRow(truth.SourceFile, templateName, fieldName, outcome.ToString(), truthField.TruthValue, extractedRaw));
                }
            }
            catch (Exception ex)
            {
                extractionFailures.Add((truth.SourceFile, ex.Message));
            }
            finally
            {
                pdfDataExtractor.Dispose();
            }
        }

        Directory.CreateDirectory(OutputService.OutputFolder!);

        var detailPath = Path.Combine(OutputService.OutputFolder!, $"_wr51-groundtruth-accuracy-detail{outputSuffix}.csv");
        await using (var writer = new StreamWriter(detailPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(detailRows);
        }

        var summaryRows = detailRows
            .GroupBy(r => r.Field)
            .Select(g =>
            {
                var truthPresent = g.Count(r => r.Outcome is "Hit" or "PartialHit" or "Miss" or "Wrong");
                var hit = g.Count(r => r.Outcome == "Hit");
                var partial = g.Count(r => r.Outcome == "PartialHit");
                var miss = g.Count(r => r.Outcome == "Miss");
                var wrong = g.Count(r => r.Outcome == "Wrong");
                var truthAbsent = g.Count(r => r.Outcome is "TrueNegative" or "Hallucination");
                var hallucination = g.Count(r => r.Outcome == "Hallucination");
                var unmodeled = g.Count(r => r.Outcome == "Unmodeled");

                var recall = truthPresent == 0 ? "n/a" : ((double)(hit + partial) / truthPresent).ToString("P0");
                var hallucinationRate = truthAbsent == 0 ? "n/a" : ((double)hallucination / truthAbsent).ToString("P0");

                return new SummaryRow(g.Key, truthPresent, hit, partial, miss, wrong, truthAbsent, hallucination, unmodeled, recall, hallucinationRate);
            })
            .OrderByDescending(r => r.TruthPresent)
            .ThenBy(r => r.Field, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var summaryPath = Path.Combine(OutputService.OutputFolder!, $"_wr51-groundtruth-accuracy-summary{outputSuffix}.csv");
        await using (var writer = new StreamWriter(summaryPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(summaryRows);
        }

        // Across all fields per template, not per field per template: most non-T1 buckets hold a
        // handful of documents (T7=1, Impounding=2), so a per-field split is single data points
        // dressed as percentages.
        var templateSummaryRows = detailRows
            .GroupBy(r => r.Template)
            .Select(g =>
            {
                var documents = g.Select(r => r.SourceFile).Distinct().Count();
                var truthPresent = g.Count(r => r.Outcome is "Hit" or "PartialHit" or "Miss" or "Wrong");
                var hit = g.Count(r => r.Outcome == "Hit");
                var partial = g.Count(r => r.Outcome == "PartialHit");
                var miss = g.Count(r => r.Outcome == "Miss");
                var wrong = g.Count(r => r.Outcome == "Wrong");
                var truthAbsent = g.Count(r => r.Outcome is "TrueNegative" or "Hallucination");
                var hallucination = g.Count(r => r.Outcome == "Hallucination");

                var recall = truthPresent == 0 ? "n/a" : ((double)(hit + partial) / truthPresent).ToString("P0");
                var hallucinationRate = truthAbsent == 0 ? "n/a" : ((double)hallucination / truthAbsent).ToString("P0");

                return new TemplateSummaryRow(g.Key, documents, truthPresent, hit, partial, miss, wrong, truthAbsent, hallucination, recall, hallucinationRate);
            })
            .OrderByDescending(r => r.Documents)
            .ToList();

        var templateSummaryPath = Path.Combine(OutputService.OutputFolder!, $"_wr51-groundtruth-accuracy-by-template{outputSuffix}.csv");
        await using (var writer = new StreamWriter(templateSummaryPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(templateSummaryRows);
        }

        var scoredDocumentCount = detailRows.Select(r => r.SourceFile).Distinct().Count();
        testOutputHelper.WriteLine($"Golden-set documents scored: {scoredDocumentCount}/{truthPaths.Length}");
        testOutputHelper.WriteLine($"Detail CSV:  {detailPath}");
        testOutputHelper.WriteLine($"Summary CSV: {summaryPath}");
        testOutputHelper.WriteLine($"By-template summary CSV: {templateSummaryPath}");

        if (fallbackTableExtractorService != null)
        {
            var fallbackRate = t1DocumentCount == 0 ? 0 : (double)fallbackUsedDocuments.Count / t1DocumentCount;
            testOutputHelper.WriteLine(
                $"Paid fallback ({fallbackTableExtractorService.Name}) used on {fallbackUsedDocuments.Count}/{t1DocumentCount} " +
                $"T1 documents ({fallbackRate:P0}) - the rest resolved via {tableExtractorService!.Name} alone, at no API cost.");
        }

        testOutputHelper.WriteLine("");
        testOutputHelper.WriteLine("Accuracy by template (all fields combined - small n outside T1, read accordingly):");
        foreach (var row in templateSummaryRows)
        {
            testOutputHelper.WriteLine(
                $"  {row.Template,-20} docs={row.Documents,2} recall={row.Recall,5} hallucination={row.HallucinationRate,5} " +
                $"(Hit={row.Hit} Partial={row.PartialHit} Miss={row.Miss} Wrong={row.Wrong} Hallucination={row.Hallucination})");
        }

        if (missingPdfs.Count > 0)
        {
            testOutputHelper.WriteLine($"Skipped (source PDF not found in {pdfFolder}): {string.Join(", ", missingPdfs)}");
        }

        if (extractionFailures.Count > 0)
        {
            testOutputHelper.WriteLine("Extraction failures:");
            foreach (var (sourceFile, error) in extractionFailures)
            {
                testOutputHelper.WriteLine($"  {sourceFile}: {error}");
            }
        }

        testOutputHelper.WriteLine("");
        testOutputHelper.WriteLine($"{"Field",-55} {"Present",7} {"Hit",5} {"Part",5} {"Miss",5} {"Wrong",5} {"Recall",7} {"Halluc",7} {"Unmod",6}");
        foreach (var row in summaryRows)
        {
            testOutputHelper.WriteLine(
                $"{row.Field,-55} {row.TruthPresent,7} {row.Hit,5} {row.PartialHit,5} {row.Miss,5} {row.Wrong,5} {row.Recall,7} {row.HallucinationRate,7} {row.Unmodeled,6}");
        }

        Assert.True(detailRows.Count > 0, "No field comparisons were produced - check ground-truth folder contents and PDF availability.");

        // A partial run is the dangerous case: recall is computed over whatever survived, so a bug
        // killing most of the golden set still reports a plausible percentage over a quietly
        // smaller denominator. Only a total wipeout trips the assert above.
        var expectedDocumentCount = truthPaths.Length - missingPdfs.Count;

        Assert.True(
            scoredDocumentCount == expectedDocumentCount,
            $"Only {scoredDocumentCount} of {expectedDocumentCount} available golden-set documents were scored - "
            + "the accuracy numbers above are computed over an incomplete set and are not comparable to other runs. "
            + $"Failures:{Environment.NewLine}"
            + string.Join(Environment.NewLine, extractionFailures.Select(f => $"  {f.SourceFile}: {f.Error}")));
    }
}