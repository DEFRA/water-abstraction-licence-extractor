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
/// WRADI-415. Runs the three date rules over the whole WQ corpus and writes the result as a CSV,
/// so the dates can be compared against the ReSP version date to identify the latest permit in DMS.
///
/// This is the extract, not the measurement: <see cref="WqDateExtractionAccuracyTests"/> is what
/// says whether the rules are any good, on the 206 documents where ground truth exists. Here every
/// document is processed and nothing is scored.
///
/// The CSV is written incrementally, so a run that is stopped part way still leaves a usable file
/// covering everything processed so far. Roughly a second a document, so the full corpus of ~10,300
/// takes a few hours; set <see cref="MaxDocuments"/> to sample instead.
///
/// Skips with a message when the corpus folder is absent, so an ordinary test run of the solution
/// is unaffected.
/// </summary>
public class WqDateCorpusExtractTests(ITestOutputHelper testOutputHelper)
{
    private static string CorpusFolder => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        "Documents",
        "TestLicences",
        "WQ");

    private static string OutputPath => Path.Combine(CorpusFolder, "wq-permit-dates.csv");

    /// <summary>Cap for a sample run. int.MaxValue processes the whole corpus.</summary>
    private const int MaxDocuments = int.MaxValue;

    private const int FlushEvery = 50;

    [Fact]
    public async Task WhenCorpusIsPresent_ThenPermitDatesAreExtractedToCsv()
    {
        // Arrange
        if (!Directory.Exists(CorpusFolder))
        {
            testOutputHelper.WriteLine($"Skipped - no corpus at {CorpusFolder}");
            return;
        }

        var files = Directory.GetFiles(CorpusFolder, "*.pdf")
            .Select(Path.GetFileName)
            .Where(name => name != null)
            .Select(name => name!)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Take(MaxDocuments)
            .ToList();

        if (files.Count == 0)
        {
            testOutputHelper.WriteLine($"Skipped - no PDFs under {CorpusFolder}");
            return;
        }

        testOutputHelper.WriteLine($"Extracting dates from {files.Count} documents into {OutputPath}");

        var csv = new StringBuilder(
            "fileName,permitNumber,fileId,statusLogLatestDate,effectiveStartDate,authorisedDate," +
            "effectiveDateSource,error\n");

        var withAll = 0;
        var withNone = 0;
        var errors = 0;
        var processed = 0;

        // Act
        foreach (var fileName in files)
        {
            string? error = null;
            DateTime? statusLog = null;
            DateTime? effective = null;
            DateTime? authorised = null;
            var effectiveSource = string.Empty;

            try
            {
                var result = await WqDateExtractionAccuracyTests.ExtractDatesAsync(fileName, CorpusFolder);

                statusLog = result.StatusLogLatest;
                effective = result.EffectiveStart;
                authorised = result.Authorised;
                // Blank when there is no effective date to describe the source of.
                effectiveSource = effective == null
                    ? string.Empty
                    : result.EffectiveFromDateOfIssue ? "dateOfIssue" : "explicit";
            }
            catch (Exception exception)
            {
                error = $"{exception.GetType().Name}: {exception.Message}".Replace(',', ';');
                errors++;
            }

            if (statusLog != null && effective != null && authorised != null)
            {
                withAll++;
            }
            else if (statusLog == null && effective == null && authorised == null)
            {
                withNone++;
            }

            csv.AppendLine(string.Join(',',
                fileName,
                PermitNumberFromFileName(fileName),
                FileIdFromFileName(fileName),
                Show(statusLog),
                Show(effective),
                Show(authorised),
                effectiveSource,
                error ?? string.Empty));

            processed++;

            // Written as it goes, so a stopped run still leaves a usable file.
            if (processed % FlushEvery == 0)
            {
                await File.WriteAllTextAsync(OutputPath, csv.ToString());
                testOutputHelper.WriteLine($"  {processed} of {files.Count}");
            }
        }

        await File.WriteAllTextAsync(OutputPath, csv.ToString());

        // Assert
        testOutputHelper.WriteLine("");
        testOutputHelper.WriteLine($"Documents processed:        {processed}");
        testOutputHelper.WriteLine($"All three dates found:      {withAll} ({(double)withAll / processed:P0})");
        testOutputHelper.WriteLine($"No dates found at all:      {withNone}");
        testOutputHelper.WriteLine($"Documents that threw:       {errors}");
        testOutputHelper.WriteLine($"Written to {OutputPath}");

        Assert.True(processed > 0);
        Assert.True(File.Exists(OutputPath));
    }

    /// <summary>
    /// The ingestion convention is <c>wq__{permit}__{fileId}.pdf</c>, so both identifiers come off
    /// the name. The permit keeps whatever form the filename used; normalising it for a join against
    /// ReSP is the caller's business.
    /// </summary>
    private static string PermitNumberFromFileName(string fileName)
    {
        var parts = Path.GetFileNameWithoutExtension(fileName).Split("__");
        return parts.Length >= 2 ? parts[1] : string.Empty;
    }

    private static string FileIdFromFileName(string fileName)
    {
        var parts = Path.GetFileNameWithoutExtension(fileName).Split("__");
        return parts.Length >= 3 ? parts[^1] : string.Empty;
    }

    /// <summary>WRADI-415 asks for DD/MM/YYYY.</summary>
    private static string Show(DateTime? value) =>
        value?.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) ?? string.Empty;
}
