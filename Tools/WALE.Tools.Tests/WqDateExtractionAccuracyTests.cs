using System.Globalization;
using System.Text;
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
using WALE.Tools._2ndHalf.Configuration;
using Xunit.Abstractions;

namespace WALE.Tools.Tests;

/// <summary>
/// Accuracy harness for WQ permit date extraction. Scores what
/// <see cref="WqFormDateLabelConfiguration"/> pulls out of each document against a ground truth
/// set, so a rule change can be judged by a number instead of by eye.
///
/// THE GROUND TRUTH AND ITS LIMITS. The set is
/// <c>~/Documents/TestLicences/WQ/wq-date-ground-truth.csv</c>, outside the repo because it is
/// Environment Agency data. Its 206 rows were built by pairing the corpus in
/// <c>~/Documents/TestLicences/WQ</c> with the Permit Publishing Log sample, joining on the permit
/// number (separators and leading zeros stripped) and then on the version label printed on the
/// document's own first page. The version is independent of the dates, so linking on it and then
/// scoring dates is not circular.
///
/// Matching on a version mentioned ANYWHERE in the document does not work: the status log lists
/// historic versions too, which produced five pairs of unrelated documents. The header version is
/// the one that identifies the issue in hand.
///
/// A pair is only kept when at least one of the log's two dates is findable verbatim in the
/// document we hold. That criterion is independent of our own extraction, and it is what removes
/// documents whose version label coincides but which are plainly a different issue - one row of
/// 207 failed it and is excluded, leaving 206.
///
/// The dates themselves are the publishing pipeline's own extractions, corroborated by being
/// present in the document, NOT human-verified truth. So a disagreement is a case to adjudicate,
/// not automatically our error - the whole point of the exercise is that their dates are sometimes
/// wrong. Disagreements are written to a file for exactly that review, and the handful confirmed
/// by hand should be promoted into a human-labelled set over time.
///
/// Measuring against another extractor caps us at parity with it. Treat the score as a regression
/// guard and a disagreement generator, not as a measure of being right.
///
/// Skips with a message when the ground truth file or the corpus is absent, so an ordinary test
/// run of the solution is unaffected.
/// </summary>
public class WqDateExtractionAccuracyTests(ITestOutputHelper testOutputHelper)
{
    private static string CorpusFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents",
        "TestLicences",
        "WQ");

    private static string GroundTruthPath => Path.Combine(CorpusFolder, "wq-date-ground-truth.csv");

    /// <summary>
    /// Floors for the share of documents whose date we get exactly right, so a rule change that
    /// regresses fails here rather than being noticed later. Both fields measured at 206 of 206 on
    /// 2026-10-08; the floors sit a few documents below that so a single awkward template does not
    /// fail the build, while a real regression does. Raise them if the rules get better.
    /// </summary>
    private const double MinimumEffectiveDateAgreement = 0.97;

    private const double MinimumIssuedDateAgreement = 0.97;

    private sealed record GroundTruthRow(
        string FileName,
        string Permit,
        string Version,
        DateTime? IssuedDate,
        DateTime? EffectiveDate,
        string PermitType);

    private sealed record Scored(
        GroundTruthRow Truth,
        DateTime? ExtractedIssued,
        DateTime? ExtractedEffective,
        string? Error);

    private static List<GroundTruthRow> ReadGroundTruth()
    {
        var lines = File.ReadAllLines(GroundTruthPath);
        var header = lines[0].Split(',');
        var index = header
            .Select((name, position) => (name, position))
            .ToDictionary(entry => entry.name, entry => entry.position);

        var rows = new List<GroundTruthRow>();

        foreach (var line in lines.Skip(1))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            // The generated file has no quoted fields, so a plain split is safe here.
            var cells = line.Split(',');

            rows.Add(new GroundTruthRow(
                cells[index["fileName"]],
                cells[index["permit"]],
                cells[index["version"]],
                ParseDate(cells[index["issuedDate"]]),
                ParseDate(cells[index["effectiveDate"]]),
                cells[index["permitType"]]));
        }

        return rows;
    }

    private static DateTime? ParseDate(string value)
    {
        return DateTime.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var parsed)
            ? parsed.Date
            : null;
    }

    /// <summary>
    /// Runs the real extraction pipeline over one document, so the harness measures the same path
    /// production would use rather than a test-only shortcut.
    /// </summary>
    private static async Task<MatchesResult?> ExtractAsync(string fileName)
    {
        var folder = CorpusFolder.EndsWith('/') ? CorpusFolder : CorpusFolder + "/";

        var fileService = new LocalFileService(folder);
        var cacheService = new FileSystemCacheService("Cache/");
        var outputService = new FileSystemOutputService("Output/");

        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>(),
            cacheService,
            outputService,
            new PdfPigNoOcrPdfDocumentService(),
            new DocnetNoOcrAlternativePdfDocumentService(),
            new ApiMessageQueueService(new HttpClient()));

        var lookupConfiguration = new LookupConfiguration(
            WqFormDateLabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            null!,
            null!,
            GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            skipFileIfMoreThenPages: 100,
            useLockExclusivity: false);

        var (_, _, matchesResult) = await pdfDataExtractor.GetMatchesAsync(
            fileName,
            new DmsFileData { FileId = Guid.NewGuid() },
            lookupConfiguration,
            [fileName],
            -1);

        return matchesResult;
    }

    /// <summary>
    /// Pulls the first parseable date out of a label group's matched text. The rules ask for a Date
    /// format, but the matched text can still carry surrounding words, so each line is scanned.
    /// </summary>
    private static DateTime? DateFromGroup(MatchesResult result, string labelGroupName)
    {
        var matches = result.Matches?
            .Where(match => match.LabelGroupName == labelGroupName)
            .ToList();

        if (matches == null || matches.Count == 0)
        {
            return null;
        }

        foreach (var text in matches
            .SelectMany(match => match.Text ?? [])
            .Select(line => line.Text)
            .Where(line => !string.IsNullOrWhiteSpace(line)))
        {
            var found = FirstDateIn(text!);

            if (found != null)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Whether a label group matched at all, regardless of what text it captured.</summary>
    private static bool GroupMatched(MatchesResult result, string labelGroupName)
    {
        return result.Matches?.Any(match => match.LabelGroupName == labelGroupName) == true;
    }

    private static DateTime? FirstDateIn(string text)
    {
        var formats = new[]
        {
            "dd/MM/yyyy", "d/M/yyyy", "dd.MM.yyyy", "d.M.yyyy",
            "d MMMM yyyy", "dd MMMM yyyy", "yyyy-MM-dd"
        };

        foreach (var rawToken in text.Split([' ', '\t', ',', ';', '(', ')'], StringSplitOptions.RemoveEmptyEntries))
        {
            // The sentence form ends the date with a full stop ("take effect from 15/09/2025."), so
            // surrounding punctuation has to come off before parsing. A trailing dot cannot be part
            // of a date here: the dotted form dd.MM.yyyy always ends in a digit.
            var token = rawToken.Trim('.', ':', '-', '\'', '"', '[', ']');

            if (DateTime.TryParseExact(token, formats, CultureInfo.InvariantCulture,
                    DateTimeStyles.None, out var parsed))
            {
                return parsed.Date;
            }
        }

        // The written form spans three tokens, so try the whole string too.
        return DateTime.TryParseExact(text.Trim(), formats, CultureInfo.InvariantCulture,
            DateTimeStyles.None, out var wholeLine)
            ? wholeLine.Date
            : null;
    }

    [Fact]
    public async Task WhenGroundTruthIsPresent_ThenWqDateExtractionIsScored()
    {
        // Arrange
        if (!File.Exists(GroundTruthPath))
        {
            testOutputHelper.WriteLine($"Skipped - no ground truth set at {GroundTruthPath}");
            return;
        }

        var truth = ReadGroundTruth();
        var present = truth.Where(row => File.Exists(Path.Combine(CorpusFolder, row.FileName))).ToList();

        testOutputHelper.WriteLine($"Ground truth rows: {truth.Count}, of which on disk: {present.Count}");

        if (present.Count == 0)
        {
            testOutputHelper.WriteLine($"Skipped - no ground truth documents found under {CorpusFolder}");
            return;
        }

        var scored = new List<Scored>();

        // Act - deliberately sequential. The extraction pipeline shares a file system cache, and a
        // readable per-document trace matters more here than wall clock.
        foreach (var row in present)
        {
            try
            {
                var result = await ExtractAsync(row.FileName);

                if (result == null)
                {
                    scored.Add(new Scored(row, null, null, "no result"));
                    continue;
                }

                var issuedDate = DateFromGroup(result, WqFormDateLabelConfiguration.IssuedDateLabelGroup);
                var effectiveDate = DateFromGroup(result, WqFormDateLabelConfiguration.EffectiveDateLabelGroup);

                // "The notice shall take effect from the date of issue" names no date, so the
                // effective date is the issued one. Substituting it knowingly is the document's own
                // statement, not a guess.
                if (effectiveDate == null
                    && issuedDate != null
                    && GroupMatched(result, WqFormDateLabelConfiguration.EffectiveOnIssueLabelGroup))
                {
                    effectiveDate = issuedDate;
                }

                scored.Add(new Scored(row, issuedDate, effectiveDate, null));
            }
            catch (Exception exception)
            {
                scored.Add(new Scored(row, null, null, $"{exception.GetType().Name}: {exception.Message}"));
            }

            if (scored.Count % 25 == 0)
            {
                testOutputHelper.WriteLine($"  scored {scored.Count} of {present.Count}");
            }
        }

        // Assert
        var effective = Tally(scored, s => s.Truth.EffectiveDate, s => s.ExtractedEffective);
        var issued = Tally(scored, s => s.Truth.IssuedDate, s => s.ExtractedIssued);

        testOutputHelper.WriteLine("");
        testOutputHelper.WriteLine($"Effective date  {Describe(effective, scored.Count)}");
        testOutputHelper.WriteLine($"Issued date     {Describe(issued, scored.Count)}");

        var errors = scored.Count(s => s.Error != null);

        if (errors > 0)
        {
            testOutputHelper.WriteLine($"Documents that threw: {errors}");
        }

        await WriteReportsAsync(scored);

        Assert.True(
            effective.Agreed >= MinimumEffectiveDateAgreement * scored.Count,
            $"Effective date agreement {effective.Agreed}/{scored.Count} fell below the floor of " +
            $"{MinimumEffectiveDateAgreement:P0}");

        Assert.True(
            issued.Agreed >= MinimumIssuedDateAgreement * scored.Count,
            $"Issued date agreement {issued.Agreed}/{scored.Count} fell below the floor of " +
            $"{MinimumIssuedDateAgreement:P0}");
    }

    private static (int Agreed, int Disagreed, int NotFound) Tally(
        List<Scored> scored,
        Func<Scored, DateTime?> expected,
        Func<Scored, DateTime?> actual)
    {
        var agreed = 0;
        var disagreed = 0;
        var notFound = 0;

        foreach (var row in scored)
        {
            var want = expected(row);
            var got = actual(row);

            if (got == null)
            {
                notFound++;
            }
            else if (want == got)
            {
                agreed++;
            }
            else
            {
                disagreed++;
            }
        }

        return (agreed, disagreed, notFound);
    }

    private static string Describe((int Agreed, int Disagreed, int NotFound) tally, int total)
    {
        return $"agreed {tally.Agreed} ({(double)tally.Agreed / total:P0})  " +
            $"disagreed {tally.Disagreed}  nothing extracted {tally.NotFound}  of {total}";
    }

    /// <summary>
    /// Writes a per-document result file and a disagreement file. xUnit's console logger does not
    /// reliably surface a long run's output, and the disagreements are the working list: each one is
    /// either a rule gap on our side or a wrong date on theirs, and only reading the document says
    /// which.
    /// </summary>
    private static async Task WriteReportsAsync(List<Scored> scored)
    {
        var folder = Path.Combine(AppContext.BaseDirectory, "WqDateAccuracy");
        Directory.CreateDirectory(folder);

        var results = new StringBuilder(
            "fileName,permit,version,permitType,truthIssued,extractedIssued,truthEffective,extractedEffective,error\n");

        var disagreements = new StringBuilder(
            "fileName,permit,version,permitType,field,truth,extracted\n");

        foreach (var row in scored.OrderBy(s => s.Truth.FileName))
        {
            results.AppendLine(string.Join(',',
                row.Truth.FileName,
                row.Truth.Permit,
                row.Truth.Version,
                row.Truth.PermitType,
                Show(row.Truth.IssuedDate),
                Show(row.ExtractedIssued),
                Show(row.Truth.EffectiveDate),
                Show(row.ExtractedEffective),
                row.Error ?? string.Empty));

            if (row.ExtractedEffective != null && row.ExtractedEffective != row.Truth.EffectiveDate)
            {
                disagreements.AppendLine(string.Join(',',
                    row.Truth.FileName, row.Truth.Permit, row.Truth.Version, row.Truth.PermitType,
                    "effective", Show(row.Truth.EffectiveDate), Show(row.ExtractedEffective)));
            }

            if (row.ExtractedIssued != null && row.ExtractedIssued != row.Truth.IssuedDate)
            {
                disagreements.AppendLine(string.Join(',',
                    row.Truth.FileName, row.Truth.Permit, row.Truth.Version, row.Truth.PermitType,
                    "issued", Show(row.Truth.IssuedDate), Show(row.ExtractedIssued)));
            }
        }

        await File.WriteAllTextAsync(Path.Combine(folder, "results.csv"), results.ToString());
        await File.WriteAllTextAsync(Path.Combine(folder, "disagreements.csv"), disagreements.ToString());
    }

    private static string Show(DateTime? value) => value?.ToString("yyyy-MM-dd") ?? string.Empty;
}
