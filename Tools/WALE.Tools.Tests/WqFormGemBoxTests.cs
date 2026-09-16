using System.Text.RegularExpressions;
using GemBox.Pdf;
using GemBox.Pdf.Content;
using Path = System.IO.Path;
using PdfDocument = GemBox.Pdf.PdfDocument;
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
/// Trial of GemBox.Pdf's PdfText.Find/Replace API - architecturally the same per-fragment
/// find-and-replace-in-place model as WqFormAsposeTests (not remove-then-overlay, like the
/// PdfSharp and iText POCs), so expect the same "lettered-list marker gets lost" outcome unless
/// grouped differently. GemBox.Pdf's free tier limits reading/writing to 2 pages per document but
/// is otherwise full-featured and licensed for commercial use - both real sample files' matched
/// blocks fit that limit (WQ__002671 spans 2 pages, WQ__003101 just 1), so this reuses the same
/// page-extraction workaround built for the Aspose trial, this time because the free tier requires
/// it rather than because evaluation mode blocks page access outright.
/// </summary>
public class WqFormGemBoxTests(ITestOutputHelper testOutputHelper)
{
    static WqFormGemBoxTests()
    {
        ComponentInfo.SetLicense("FREE-LIMITED-KEY");
    }

    [Theory]
    [MemberData(nameof(WqFormParagraphOverlayTests.SampleFiles), MemberType = typeof(WqFormParagraphOverlayTests))]
    public async Task WhenRealWqFormFile_ThenGemBoxReplacesTextInPlace(string sourcePath)
    {
        var folder = Path.GetDirectoryName(sourcePath)!;
        var filename = Path.GetFileName(sourcePath);

        if (!folder.EndsWith('/'))
        {
            folder += "/";
        }

        var fileService = new LocalFileService(folder);
        var cacheService = new FileSystemCacheService("Cache/");
        var outputService = new FileSystemOutputService("Output/");
        var documentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        var messageQueueService = new ApiMessageQueueService(new HttpClient());

        var pdfDataExtractor = new PdfDataExtractorService(
            new PdfPigNoOcrDataExtractorService(),
            new List<IOcrDataExtractorService>(),
            cacheService,
            outputService,
            documentService,
            docnetAlternativeDocumentService,
            messageQueueService);

        var lookupConfiguration = new LookupConfiguration(
            WqFormLabelConfiguration.GetLabels(),
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

        var (stopExecution, _, matchesResult) = await pdfDataExtractor.GetMatchesAsync(
            filename,
            new DmsFileData { FileId = Guid.NewGuid() },
            lookupConfiguration,
            [filename],
            -1);

        Assert.False(stopExecution);
        Assert.NotNull(matchesResult);

        var wholeBlockMatch = matchesResult.Matches?
            .FirstOrDefault(match => match.LabelGroupName == "SpecialTermsWholeBlock");

        if (wholeBlockMatch?.Text == null || wholeBlockMatch.Text.Count == 0)
        {
            testOutputHelper.WriteLine($"No SpecialTermsWholeBlock match found in {filename} - nothing to replace.");
            return;
        }

        var comparisonFolder = Path.Combine(AppContext.BaseDirectory, "ComparisonOutput");
        Directory.CreateDirectory(comparisonFolder);

        var originalCopyPath = Path.Combine(
            comparisonFolder,
            $"{Path.GetFileNameWithoutExtension(filename)}-original.pdf");
        File.Copy(sourcePath, originalCopyPath, overwrite: true);

        var matchedLines = WqFormParagraphOverlayTests.RemoveAccidentalOutliers(wholeBlockMatch.Text);

        var outputPath = Path.Combine(
            comparisonFolder,
            $"{Path.GetFileNameWithoutExtension(filename)}-gembox-replaced.pdf");

        ReplaceLinesInPlace(sourcePath, outputPath, matchedLines, new WqFormParagraphOverlayTests.FillerTextCursor());

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote GemBox in-place-replace PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

    /// <summary>
    /// Replaces one whole clause (a marker line plus any wrapped continuation lines) by
    /// distributing new filler text back across those same lines' matches, one chunk per line.
    /// </summary>
    /// <remarks>
    /// Same two-stage fix as WqFormAsposeTests.ReplaceLinesInPlace. Putting the whole clause's new
    /// text on just the marker line's match (an earlier version of this method) restored the "(a) "
    /// marker but exposed that PdfText.Replace doesn't wrap either - it just ran the combined text
    /// off the page edge unwrapped, confirmed via pdftotext. Fixed the same way: manually
    /// word-wrapping the filler text into as many chunks as the clause has lines (each line's
    /// character budget taken from its own original text length - the real wrap point this
    /// document's own renderer already chose), then giving each line's match its own chunk.
    /// </remarks>
    private void ReplaceLinesInPlace(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines,
        WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
        var (extractedPath, pageNumberMap) = ExtractMatchedPages(sourcePath, matchedLines);

        using var document = PdfDocument.Load(extractedPath);

        var itemGroups = WqFormParagraphOverlayTests.GroupLinesByMarker(matchedLines);

        foreach (var itemLines in itemGroups)
        {
            var markerMatch = ItemMarkerRegex.Match(itemLines[0].Text);
            var prefix = markerMatch.Success ? $"({markerMatch.Groups[1].Value}) " : string.Empty;

            var lineBudgets = itemLines.Select(line => Math.Max(line.Text.Length, 10)).ToList();
            lineBudgets[0] = Math.Max(lineBudgets[0] - prefix.Length, 10);

            var fillerText = fillerCursor.Next(lineBudgets.Sum());
            var chunks = SplitIntoChunks(fillerText, lineBudgets);

            for (var index = 0; index < itemLines.Count; index++)
            {
                var chunk = chunks[index];
                var line = itemLines[index];

                if (index == 0)
                {
                    FindLine(document, pageNumberMap, line)?.Replace(prefix + chunk);
                }
                else if (string.IsNullOrEmpty(chunk))
                {
                    FindLine(document, pageNumberMap, line)?.Redact();
                }
                else
                {
                    FindLine(document, pageNumberMap, line)?.Replace(chunk);
                }
            }
        }

        document.Save(outputPath);
    }

    /// <summary>
    /// Greedily word-wraps <paramref name="text"/> into as many chunks as there are budgets,
    /// packing words into each chunk up to its character budget. Any words left over once every
    /// budget is used are appended to the last chunk rather than dropped.
    /// </summary>
    private static List<string> SplitIntoChunks(string text, IReadOnlyList<int> lineBudgets)
    {
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<string>();
        var wordIndex = 0;

        foreach (var budget in lineBudgets)
        {
            var chunkWords = new List<string>();
            var currentLength = 0;

            while (wordIndex < words.Length)
            {
                var word = words[wordIndex];
                var candidateLength = currentLength == 0 ? word.Length : currentLength + 1 + word.Length;

                if (currentLength > 0 && candidateLength > budget)
                {
                    break;
                }

                chunkWords.Add(word);
                currentLength = candidateLength;
                wordIndex++;
            }

            chunks.Add(string.Join(' ', chunkWords));
        }

        if (wordIndex < words.Length)
        {
            var leftover = string.Join(' ', words.Skip(wordIndex));
            chunks[^1] = string.IsNullOrEmpty(chunks[^1]) ? leftover : $"{chunks[^1]} {leftover}";
        }

        return chunks;
    }

    private GemBox.Pdf.Content.PdfText? FindLine(
        PdfDocument document, IReadOnlyDictionary<int, int> pageNumberMap, DocumentLine line)
    {
        if (string.IsNullOrWhiteSpace(line.Text))
        {
            return null;
        }

        var page = document.Pages[pageNumberMap[line.PageNumber] - 1];
        var pattern = BuildWhitespaceTolerantPattern(line.Text);
        var match = page.Content.GetText().Find(new Regex(pattern)).FirstOrDefault();

        if (match == null)
        {
            testOutputHelper.WriteLine($"  NOT FOUND page={line.PageNumber}: \"{line.Text}\"");
        }

        return match;
    }

    /// <summary>
    /// Copies only the pages containing matched lines into a new standalone PDF (via PdfSharp),
    /// returning its path and a map from original page number to the new document's page number -
    /// same technique as WqFormAsposeTests.ExtractMatchedPages.
    /// </summary>
    private static (string ExtractedPath, Dictionary<int, int> PageNumberMap) ExtractMatchedPages(
        string sourcePath, IReadOnlyList<DocumentLine> matchedLines)
    {
        var originalPageNumbers = matchedLines
            .Select(line => line.PageNumber)
            .Distinct()
            .OrderBy(pageNumber => pageNumber)
            .ToList();

        using var sourceDocument = PdfSharp.Pdf.IO.PdfReader.Open(
            sourcePath, PdfSharp.Pdf.IO.PdfDocumentOpenMode.Import);
        using var extracted = new PdfSharp.Pdf.PdfDocument();

        var pageNumberMap = new Dictionary<int, int>();

        foreach (var originalPageNumber in originalPageNumbers)
        {
            extracted.AddPage(sourceDocument.Pages[originalPageNumber - 1]);
            pageNumberMap[originalPageNumber] = pageNumberMap.Count + 1;
        }

        var extractedPath = Path.Combine(
            AppContext.BaseDirectory,
            "ComparisonOutput",
            $"{Path.GetFileNameWithoutExtension(sourcePath)}-gembox-extracted-pages.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(extractedPath)!);
        extracted.Save(extractedPath);

        return (extractedPath, pageNumberMap);
    }

    /// <summary>
    /// Builds a regex matching the given line's words in order with flexible whitespace between
    /// them, and either straight or curly quote characters wherever the line has an apostrophe or
    /// quote mark - same technique (and same reason) as WqFormITextSweepTests/WqFormAsposeTests.
    /// </summary>
    private static string BuildWhitespaceTolerantPattern(string lineText)
    {
        var words = lineText
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(word => Regex.Escape(word)
                .Replace("'", "['‘’]")
                .Replace("\"", "[\"“”]"));

        return string.Join(@"\s+", words);
    }
}
