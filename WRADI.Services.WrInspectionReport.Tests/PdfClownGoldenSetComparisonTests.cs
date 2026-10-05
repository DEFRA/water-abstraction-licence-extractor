using System.Text.Json;
using System.Text.RegularExpressions;
using org.pdfclown.documents.contents;
using org.pdfclown.documents.contents.objects;
using UglyToad.PdfPig;
using WRADI.Services.WrInspectionReport.Tests.Config;
using Xunit.Abstractions;
using static WALE.ProcessFile.Services.PdfClown.GridCellReconstructor;
using static WALE.ProcessFile.Services.PdfClown.WordToCellAssigner;
using PdfClownPath = org.pdfclown.documents.contents.objects.Path;

namespace WRADI.Services.WrInspectionReport.Tests;

/// <summary>
/// Scores the PdfClown grid-extraction approach against the same golden set and the same
/// Classify()/Outcome rules as Wr51GroundTruthAccuracyTests, so the numbers compare directly.
///
/// A feasibility measurement, not a competing production extractor, so field coverage is partial:
/// everything reachable by "find the cell whose text starts with this label, strip it" on page 1 -
/// the header block, the 13 LicenceProvisions grid fields, and the simple MeasurementDetails
/// label+value fields. Excluded: GeneralComments and FormSentTo/Date (usually page 2+, and
/// GeneralComments has many heading variants), the Calibration/Conformance/FlowVerification/
/// MeterVerification/Verification and Maintenance/ReadingsTaken fields (value sits in a separate
/// adjacent tick cell - reachable, not attempted), and the already-Unmodeled certificate fields.
/// </summary>
public class PdfClownGoldenSetComparisonTests(ITestOutputHelper output)
{
    private static readonly string GroundTruthFolder = $"{TestConfig.PdfFolder}/truth";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private record Segment(double X, double Y, double Width, double Height)
    {
        public bool IsHorizontal => Width >= Height;
    }

    private record DetailRow(string SourceFile, string Field, Wr51GroundTruthAccuracyTests.Outcome Outcome);

    private record SummaryRow(
        string Field, int TruthPresent, int Hit, int PartialHit, int Miss, int Wrong,
        int TruthAbsent, int Hallucination, string Recall, string HallucinationRate);

    [Fact]
    public async Task WhenScoringAgainstHandLabelledGoldenSet_ThenReportsPerFieldAccuracy()
    {
        if (!Directory.Exists(GroundTruthFolder))
        {
            output.WriteLine($"Ground-truth folder not found at {GroundTruthFolder} - skipping.");
            return;
        }

        var truthPaths = Directory.GetFiles(GroundTruthFolder, "*.truth.json");
        Assert.True(truthPaths.Length > 0, $"No .truth.json files found in {GroundTruthFolder}");

        var detailRows = new List<DetailRow>();
        var skipped = new List<(string File, string Reason)>();

        foreach (var truthPath in truthPaths.OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var truth = JsonSerializer.Deserialize<Wr51GroundTruthAccuracyTests.TruthFile>(
                await File.ReadAllTextAsync(truthPath), JsonOptions);

            if (truth?.SourceFile == null)
            {
                continue;
            }

            var pdfPath = System.IO.Path.Combine(TestConfig.PdfFolder, truth.SourceFile);

            if (!File.Exists(pdfPath))
            {
                skipped.Add((truth.SourceFile, "PDF not found"));
                continue;
            }

            List<Segment> segments;

            try
            {
                segments = ExtractSegments(pdfPath);
            }
            catch (Exception ex)
            {
                skipped.Add((truth.SourceFile, $"PdfClown: {ex.GetType().Name}"));
                continue;
            }

            var cells = ReconstructCells(segments.Select(s => new BorderSegment(s.X, s.Y, s.Width, s.Height)))
                .ToList();

            List<WordBox> words;

            try
            {
                using var pdfPigDoc = PdfDocument.Open(pdfPath);
                words = pdfPigDoc.GetPage(1).GetWords()
                    .Select(w => new WordBox(
                        w.Text, w.BoundingBox.Left, w.BoundingBox.Right, w.BoundingBox.Top, w.BoundingBox.Bottom))
                    .ToList();
            }
            catch (Exception ex)
            {
                skipped.Add((truth.SourceFile, $"PdfPig: {ex.GetType().Name}"));
                continue;
            }

            var populated = AssignWordsToCells(cells, words)
                .Where(kv => kv.Value.Count > 0)
                .Select(kv => CellText(kv.Value))
                .ToList();

            foreach (var (fieldName, extractor) in FieldExtractors)
            {
                if (!truth.Fields.TryGetValue(fieldName, out var truthField))
                {
                    continue;
                }

                var extracted = extractor(populated);
                var outcome = Wr51GroundTruthAccuracyTests.Classify(fieldName, truthField, extracted);
                detailRows.Add(new DetailRow(truth.SourceFile, fieldName, outcome));
            }
        }

        output.WriteLine($"{truthPaths.Length} ground-truth documents, {skipped.Count} skipped, " +
                          $"{detailRows.Count} field scores across {FieldExtractors.Count} fields covered");

        if (skipped.Count > 0)
        {
            output.WriteLine("Skipped:");
            foreach (var (file, reason) in skipped)
            {
                output.WriteLine($"  {file}: {reason}");
            }
        }

        var summary = detailRows
            .GroupBy(r => r.Field)
            .Select(g =>
            {
                var truthPresent = g.Count(r => r.Outcome is not Wr51GroundTruthAccuracyTests.Outcome.TrueNegative
                    and not Wr51GroundTruthAccuracyTests.Outcome.Hallucination);
                var hit = g.Count(r => r.Outcome == Wr51GroundTruthAccuracyTests.Outcome.Hit);
                var partialHit = g.Count(r => r.Outcome == Wr51GroundTruthAccuracyTests.Outcome.PartialHit);
                var miss = g.Count(r => r.Outcome == Wr51GroundTruthAccuracyTests.Outcome.Miss);
                var wrong = g.Count(r => r.Outcome == Wr51GroundTruthAccuracyTests.Outcome.Wrong);
                var truthAbsent = g.Count(r => r.Outcome is Wr51GroundTruthAccuracyTests.Outcome.TrueNegative
                    or Wr51GroundTruthAccuracyTests.Outcome.Hallucination);
                var hallucination = g.Count(r => r.Outcome == Wr51GroundTruthAccuracyTests.Outcome.Hallucination);

                var recall = truthPresent > 0 ? $"{(double)(hit + partialHit) / truthPresent:P0}" : "n/a";
                var hallucinationRate = truthAbsent > 0 ? $"{(double)hallucination / truthAbsent:P0}" : "n/a";

                return new SummaryRow(g.Key, truthPresent, hit, partialHit, miss, wrong, truthAbsent,
                    hallucination, recall, hallucinationRate);
            })
            .OrderByDescending(r => r.TruthPresent)
            .ToList();

        output.WriteLine("");
        output.WriteLine("Field,TruthPresent,Hit,PartialHit,Miss,Wrong,TruthAbsent,Hallucination,Recall,HallucinationRate");

        foreach (var row in summary)
        {
            output.WriteLine($"{row.Field},{row.TruthPresent},{row.Hit},{row.PartialHit},{row.Miss},{row.Wrong}," +
                              $"{row.TruthAbsent},{row.Hallucination},{row.Recall},{row.HallucinationRate}");
        }

        Assert.NotEmpty(summary);
    }

    // Anchor label -> extracted value. Tries each variant in order, since templates word the same
    // field differently; the first cell starting with one wins.
    private static readonly Dictionary<string, Func<List<string>, string?>> FieldExtractors = new()
    {
        ["LicenceNumber"] = cells => FindValue(cells,
            "Licence No. (or Application No. or GIC No. etc.)", "Licence No:", "Licence No."),
        ["InspectionClass"] = cells => FindValue(cells, "Inspection Class:"),
        ["Address.NameAndAddress"] = cells => FindValue(cells,
            "Name and address:", "Permit holder name and address:"),
        ["Address.TelephoneNumber"] = cells => FindValue(cells, "Telephone No:", "Telephone No"),
        ["Address.SiteAddress"] = cells => FindValue(cells, "Site address (if different):"),
        ["MetWith.Name"] = cells => FindValue(cells, "Met with:"),
        ["MetWith.Position"] = cells => FindValue(cells, "Position:"),
        ["InspectingOfficer"] = cells => FindValue(cells, "Inspecting Officer:", "Inspecting Officers:"),
        ["InspectionDate"] = cells => NormalizeDate(FindValue(cells, "Inspection Date:")),
        ["Time"] = cells => FindValue(cells, "Time:"),
        ["LicenceProvisions.SourceOfSupply"] = cells => InOrder(FindValue(cells, "Source of supply:")),
        ["LicenceProvisions.PointOfAbstraction"] = cells => InOrder(FindValue(cells, "Point of abstraction:")),
        ["LicenceProvisions.MeansOfAbstraction"] = cells => InOrder(FindValue(cells, "Means of abstraction:")),
        ["LicenceProvisions.Purposes"] = cells => InOrder(FindValue(cells, "Purpose(s):")),
        ["LicenceProvisions.Period"] = cells => InOrder(FindValue(cells, "Period:")),
        ["LicenceProvisions.Quantities"] = cells => InOrder(FindValue(cells, "Quantities:")),
        ["LicenceProvisions.MeansOfMeasurement"] = cells => InOrder(FindValue(cells, "Means of measurement:")),
        ["LicenceProvisions.Records"] = cells => InOrder(FindValue(cells, "Records:", "R ecords:", "R e c o rds:")),
        ["LicenceProvisions.ProvisionOfInformation"] = cells =>
            InOrder(FindValue(cells, "Provision of information:", "Provision of information")),
        ["LicenceProvisions.SpecialConditions"] = cells => InOrder(FindValue(cells, "Special conditions:")),
        ["LicenceProvisions.Land"] = cells => InOrder(FindValue(cells, "Land (only if specified):")),
        ["LicenceProvisions.ChargingFactors"] = cells => InOrder(FindValue(cells, "Charging factors:")),
        ["LicenceProvisions.OtherProvisions"] = cells => InOrder(FindValue(cells, "Other provisions (specify below):")),
        ["MeasurementDetails.MeterMake"] = cells => FindValue(cells, "Meter make:"),
        ["MeasurementDetails.SerialNumber"] = cells => FindValue(cells, "Serial number:", "Serial number"),
        ["MeasurementDetails.Reading"] = cells => FindValue(cells, "Reading:", "Reading"),
        ["MeasurementDetails.Units"] = cells => FindValue(cells, "Units:", "Units"),
        ["MeasurementDetails.Other"] = cells => FindValue(cells, "Other:"),
        ["MeasurementDetails.CertificatesOrRecordsAvailableFor"] = cells =>
            FindValue(cells, "Certificates or records available for:", "Certificates or records available:"),
        ["MeasurementDetails.DateOfCertificateOrRecord"] = cells =>
            NormalizeDate(FindValue(cells, "Date of certificate or record:")),
        ["MeasurementDetails.WhereKept"] = cells => FindValue(cells, "Where kept:")
    };

    private static string? FindValue(List<string> cellTexts, params string[] labelVariants)
    {
        foreach (var label in labelVariants)
        {
            var match = cellTexts.FirstOrDefault(t => t.StartsWith(label, StringComparison.OrdinalIgnoreCase));

            if (match == null)
            {
                continue;
            }

            var stripped = match[label.Length..].TrimStart(':', ' ', '-').Trim();
            return string.IsNullOrWhiteSpace(stripped) ? null : stripped;
        }

        return null;
    }

    // 🗸 (U+1F5F8) excluded: a surrogate pair, not a single UTF-16 char, same reason
    // FormattingHelper's trim guard excludes it.
    private static readonly char[] TickGlyphs = ['✓', '✔', '√', '', '', '', '', ''];

    private static string? InOrder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();

        if (trimmed.Any(TickGlyphs.Contains))
        {
            return "InOrder";
        }

        if (trimmed.Equals("X", StringComparison.OrdinalIgnoreCase))
        {
            return "NotInOrder";
        }

        if (trimmed.Equals("n/a", StringComparison.OrdinalIgnoreCase)
            || trimmed.Equals("N/A", StringComparison.Ordinal))
        {
            return "NotApplicable";
        }

        if (trimmed.Equals("NI", StringComparison.Ordinal))
        {
            return "NotInspected";
        }

        // T6 uses Y/N rather than a tick/cross - same convention as InOrderPossibilities.
        if (trimmed.Equals("Y", StringComparison.Ordinal))
        {
            return "InOrder";
        }

        if (trimmed.Equals("N", StringComparison.Ordinal))
        {
            return "NotInOrder";
        }

        // Anything else is narrative prose, which InOrderStatus has no representation for either -
        // a real documented ceiling, not specific to this method. Returning the raw text rather
        // than null lets Classify score it against a narrative truth value instead of discarding a
        // correct extraction for not fitting the enum.
        return trimmed;
    }

    private static readonly Regex OrdinalSuffix = new(@"(\d+)\s*(st|nd|rd|th)\b", RegexOptions.IgnoreCase);

    private static string? NormalizeDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var cleaned = OrdinalSuffix.Replace(value, "$1");

        return DateTime.TryParse(cleaned, out var parsed) ? parsed.ToString("yyyy-MM-dd") : value;
    }

    // org.pdfclown.Version.Get caches into an unlocked static Dictionary, so opening two Files
    // concurrently can corrupt it and throw. Same lock as in PdfClownGridTableExtractorService.
    private static readonly Lock PdfClownFileOpenLock = new();

    private static List<Segment> ExtractSegments(string path)
    {
        org.pdfclown.files.File pdf;

        lock (PdfClownFileOpenLock)
        {
            pdf = new org.pdfclown.files.File(path);
        }

        using (pdf)
        {
            var page = pdf.Document.Pages[0];

            var segments = new List<Segment>();
            Scan(new ContentScanner(page), segments);
            return segments;
        }
    }

    private static void Scan(ContentScanner? scanner, List<Segment> segments)
    {
        if (scanner == null)
        {
            return;
        }

        while (scanner.MoveNext())
        {
            var current = scanner.Current;

            if (current is PdfClownPath)
            {
                CollectPathRectangles(scanner.ChildLevel, segments);
                continue;
            }

            if (current is CompositeObject)
            {
                Scan(scanner.ChildLevel, segments);
            }
        }
    }

    private static void CollectPathRectangles(ContentScanner? pathLevel, List<Segment> segments)
    {
        if (pathLevel == null)
        {
            return;
        }

        var rectangles = new List<DrawRectangle>();
        var filled = false;

        while (pathLevel.MoveNext())
        {
            switch (pathLevel.Current)
            {
                case DrawRectangle rect:
                    rectangles.Add(rect);
                    break;
                case PaintPath { Filled: true }:
                    filled = true;
                    break;
            }
        }

        if (!filled || rectangles.Count == 0)
        {
            return;
        }

        foreach (var rect in rectangles)
        {
            var p1 = pathLevel.State.UserToDeviceSpace(new System.Drawing.PointF((float)rect.X, (float)rect.Y));
            var p2 = pathLevel.State.UserToDeviceSpace(
                new System.Drawing.PointF((float)(rect.X + rect.Width), (float)(rect.Y + rect.Height)));

            var x = Math.Min(p1.X, p2.X);
            var y = Math.Min(p1.Y, p2.Y);
            var w = Math.Abs(p2.X - p1.X);
            var h = Math.Abs(p2.Y - p1.Y);

            segments.Add(new Segment(x, y, w, h));
        }
    }
}
