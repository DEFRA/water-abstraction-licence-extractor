using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
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
using WRADI.DocumentType.WrInspectionReport.Enums;
using WRADI.DocumentType.WrInspectionReport.Services;
using Xunit.Abstractions;
using Form = global::WRADI.DocumentType.WrInspectionReport.Models.WrInspectionReport;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Phase 0 accuracy harness for WR51. Ground truth is a hand-labelled golden set that
/// intentionally lives OUTSIDE this repo - the source PDFs and their derived
/// <c>.truth.json</c> files contain real names/addresses/phone numbers and must never be
/// committed. This test reads that external folder, replays extraction against the same
/// cached PdfPig text used by the other WR51 corpus tests, and reports per-field
/// precision/recall/hallucination stats - no DB, no API, replay-only.
///
/// If the ground-truth folder isn't present (any machine other than the one it was labelled
/// on, and CI), the test reports that and returns rather than failing - this is a reporting
/// tool, not a gate, until enough of the golden set exists to set real thresholds.
/// </summary>
public class Wr51GroundTruthAccuracyTests(ITestOutputHelper testOutputHelper)
{
    private static readonly string GroundTruthFolder =
        Environment.GetEnvironmentVariable("WR51_GROUND_TRUTH_FOLDER")
        ?? "/Users/edwardbutler/Documents/TestLicences/GroundTruth";

    private static readonly ICacheService CacheService = new FileSystemCacheService("Cache/");
    private static readonly IOutputService OutputService = new FileSystemOutputService("Output/");
    private static readonly INoOcrPdfDocumentService DocumentService = new PdfPigNoOcrPdfDocumentService();
    private static readonly INoOcrAlternativePdfDocumentService DocnetAlternativeDocumentService =
        new DocnetNoOcrAlternativePdfDocumentService();
    private static readonly IMessageQueueService MessageQueueService = new ApiMessageQueueService(new HttpClient());

    // Two T6-only measurement fields discovered while hand-labelling the golden set have no
    // corresponding property anywhere in WrInspectionReportMeasurementDetails.cs - a genuine
    // model gap, not an extraction bug. Reported separately as "Unmodeled" rather than scored
    // as misses, so they don't drown out fields the pipeline could plausibly get right.
    private static readonly HashSet<string> UnmodeledFields =
        ["MeasurementDetails.CalibrationCertificate", "MeasurementDetails.VerificationCertificate"];

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

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

    /// <summary>
    /// InOrderStatus.Blank/Unknown/DidntMatch are the enum's "nothing usable was produced"
    /// states, not real answers - ToString()-ing them directly would make every one of them
    /// look like a confidently extracted (and wrong/hallucinated) value. Collapsed to null so
    /// they're scored as "no extraction", same as a genuinely empty string field.
    /// </summary>
    // internal, not private: WrInspectionReportPdfPigNoOcrPdfTests reuses these two for its
    // full-corpus per-template coverage report, rather than duplicating the 50-field mapping.
    internal static string? FormatInOrderStatus(InOrderStatus status) =>
        status is InOrderStatus.Blank or InOrderStatus.Unknown or InOrderStatus.DidntMatch
            ? null
            : status.ToString();

    /// <summary>
    /// Property-path extractors matching the 53 ground-truth field keys. Enum fields (the
    /// LicenceProvisions grid) are compared via FormatInOrderStatus() against the truth's
    /// status string - when truth instead holds narrative text (several real documents
    /// describe provisions in prose rather than a tick mark - see the golden set's own notes)
    /// this will correctly score as a miss/wrong, which is real signal: the current model has
    /// no way to represent a narrative provisions answer, only InOrderStatus.
    /// </summary>
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
        ["MeasurementDetails.MeterName"] = f => f.MeasurementDetails.MeterName,
        ["MeasurementDetails.MeterMake"] = f => f.MeasurementDetails.MeterMake,
        ["MeasurementDetails.SerialNumber"] = f => f.MeasurementDetails.SerialNumber,
        ["MeasurementDetails.MeterAssetNumber"] = f => f.MeasurementDetails.MeterAssetNumber,
        ["MeasurementDetails.Reading"] = f => f.MeasurementDetails.Reading,
        ["MeasurementDetails.FlowRate"] = f => f.MeasurementDetails.FlowRate,
        ["MeasurementDetails.Units"] = f => f.MeasurementDetails.Units,
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

    /// <summary>
    /// Multi-line values (addresses, most often) get joined with '\n' by the pipeline but were
    /// hand-transcribed into ground truth as ", "-separated prose for readability - genuinely
    /// the same content, different join character. Stripping whitespace AND commas collapses
    /// both styles to the same bag of characters so this doesn't score as a wrong answer.
    /// </summary>
    private static string? StripSeparators(string? normalizedValue) =>
        normalizedValue == null ? null : Regex.Replace(normalizedValue, @"[\s,]+", "");

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

        // Whitespace-insensitive equality first: values like phone numbers or licence numbers
        // routinely differ only by internal spacing between truth and extraction (e.g.
        // "01730 813439" vs "01730813439") - genuinely the same value, not a wrong answer.
        // Scoring these as Wrong would misreport a harness normalization gap as a pipeline bug.
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

        return Outcome.Wrong;
    }

    [Fact]
    public async Task WhenScoringAgainstHandLabelledGoldenSet_ThenReportsPerFieldAccuracy()
    {
        if (!Directory.Exists(GroundTruthFolder))
        {
            testOutputHelper.WriteLine(
                $"Ground-truth folder not found at {GroundTruthFolder} - this is an external, " +
                "non-git-tracked golden set that only exists on the machine it was labelled on. " +
                "Set WR51_GROUND_TRUTH_FOLDER to point at it, or skip this test elsewhere. Returning without failure.");
            return;
        }

        var truthPaths = Directory.GetFiles(GroundTruthFolder, "*.truth.json");
        Assert.True(truthPaths.Length > 0, $"No .truth.json files found in {GroundTruthFolder}");

        var pdfFolder = TestConfig.PdfFolder;
        var lookupConfiguration = BuildLookupConfiguration(pdfFolder);

        var detailRows = new List<DetailRow>();
        var missingPdfs = new List<string>();
        var extractionFailures = new List<(string SourceFile, string Error)>();

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

                var (stopExecution, _, matchesResult, template) = await WrInspectionReportExtractionOrchestrator.ExtractAsync(
                    truth.SourceFile,
                    dmsFileData,
                    lookupConfiguration,
                    [truth.SourceFile],
                    processRunId: -99,
                    pdfDataExtractor);

                if (stopExecution || matchesResult == null)
                {
                    extractionFailures.Add((truth.SourceFile, "Extraction reported StopExecution or returned no result"));
                    continue;
                }

                var form = WrInspectionReportSchemaConverter.ToForm(matchesResult, dmsFileData, template);
                var templateName = template.ToString();

                foreach (var (fieldName, truthField) in truth.Fields)
                {
                    string? extractedRaw = null;

                    if (!UnmodeledFields.Contains(fieldName) && !FieldExtractors.TryGetValue(fieldName, out _))
                    {
                        // Ground-truth field with no known mapping (schema drift between the
                        // golden set and this test) - surface it rather than silently skipping.
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

        var detailPath = Path.Combine(OutputService.OutputFolder!, "_wr51-groundtruth-accuracy-detail.csv");
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

        var summaryPath = Path.Combine(OutputService.OutputFolder!, "_wr51-groundtruth-accuracy-summary.csv");
        await using (var writer = new StreamWriter(summaryPath))
        await using (var csv = new CsvWriter(writer, CultureInfo.GetCultureInfo("en-GB")))
        {
            await csv.WriteRecordsAsync(summaryRows);
        }

        // Aggregate across ALL fields per document's classified template, not per-field-per-
        // template - the golden set only has a handful of documents in most non-T1 buckets
        // (T7=1, Impounding=2 at last count), so a per-field breakdown there would mostly be
        // single data points dressed up as a percentage. This is still directly useful for the
        // one thing that's actually been asked about it: does accuracy differ by template.
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

        var templateSummaryPath = Path.Combine(OutputService.OutputFolder!, "_wr51-groundtruth-accuracy-by-template.csv");
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
    }
}
