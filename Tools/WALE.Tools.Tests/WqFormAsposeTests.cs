using System.Text.RegularExpressions;
using Aspose.Pdf;
using Aspose.Pdf.Text;
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
/// Trial of Aspose.PDF's TextFragment/TextFragmentAbsorber "find and replace" API, which is
/// architecturally different from the other two POCs: rather than removing old text and drawing a
/// separate new overlay, it edits a matched TextFragment's Text property in place, with explicit
/// policies (TextReplaceOptions.FontSizeAdjustment / ReplaceAdjustment) for how to handle the
/// replacement being a different length than the original - the "richer tooling for smaller/bigger
/// replacement" question this trial exists to answer. Each lettered clause (marker line plus any
/// wrapped continuation lines) collapses onto a single replaced fragment on the marker line, with
/// the continuation lines' fragments deleted - see ReplaceLinesInPlace for why. Aspose.PDF is
/// fully commercial with no free tier - this trial runs unlicensed, so output carries an
/// evaluation watermark; not for distribution.
/// </summary>
public class WqFormAsposeTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [MemberData(nameof(WqFormParagraphOverlayTests.SampleFiles), MemberType = typeof(WqFormParagraphOverlayTests))]
    public async Task WhenRealWqFormFile_ThenAsposeReplacesTextInPlace(string sourcePath)
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
            $"{Path.GetFileNameWithoutExtension(filename)}-aspose-replaced.pdf");

        ReplaceLinesInPlace(sourcePath, outputPath, matchedLines, new WqFormParagraphOverlayTests.FillerTextCursor());

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote Aspose in-place-replace PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

    /// <summary>
    /// Replaces one whole clause (a marker line plus any wrapped continuation lines) by
    /// distributing new filler text back across those same lines' fragments, one chunk per line.
    /// </summary>
    /// <remarks>
    /// Went through two earlier versions. First, replacing every original PdfPig line
    /// independently - including the line carrying the "(a) " marker - silently overwrote the
    /// marker along with the rest of its line, since that whole line's text got replaced. Grouping
    /// lines into clauses and putting the WHOLE new paragraph on just the marker line's fragment
    /// fixed that, but exposed a bigger problem: TextFragment.Text replacement doesn't wrap - even
    /// with FontSizeAdjustment.ShrinkToFit, cramming a 5-line clause's worth of text into one
    /// line's fragment just ran the text off the page edge unwrapped (confirmed via pdftotext, not
    /// just a render artifact).
    ///
    /// Fixed by manually word-wrapping the filler text into as many chunks as the clause has
    /// lines, then giving each original line's fragment its own chunk - the marker line gets the
    /// marker plus the first chunk, continuation lines get the rest, and any line left over once
    /// the filler text runs out gets redacted. Each line's chunk budget (in characters) reuses that
    /// line's own original text length - the real wrap point this document's own renderer already
    /// chose for this font and column width - rather than us measuring font metrics ourselves.
    ///
    /// Aspose.PDF's unlicensed evaluation mode caps any internal collection (including a document's
    /// own pages) at 4 elements - opening page 7 of these 24-26 page real files throws "At most 4
    /// elements can be viewed in evaluation mode" outright, regardless of how many pages are
    /// actually touched. Worked around by first extracting only the pages that contain matched
    /// lines (via PdfSharp, which has no such limit) into a small standalone PDF, so Aspose only
    /// ever sees 1-2 pages - a real license would remove the need for this.
    /// </remarks>
    private void ReplaceLinesInPlace(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines,
        WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
        var (extractedPath, pageNumberMap) = ExtractMatchedPages(sourcePath, matchedLines);

        using var document = new Document(extractedPath);

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
                var replacementText = index == 0 ? prefix + chunk : chunk;

                ReplaceLine(document, pageNumberMap, itemLines[index], replacementText);
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

    private void ReplaceLine(
        Document document, IReadOnlyDictionary<int, int> pageNumberMap, DocumentLine line, string replacementText)
    {
        if (string.IsNullOrWhiteSpace(line.Text))
        {
            return;
        }

        var page = document.Pages[pageNumberMap[line.PageNumber]];
        var pattern = BuildWhitespaceTolerantPattern(line.Text);
        var absorber = new TextFragmentAbsorber(new Regex(pattern))
        {
            TextReplaceOptions = new TextReplaceOptions(TextReplaceOptions.ReplaceAdjustment.None)
            {
                FontSizeAdjustmentAction = TextReplaceOptions.FontSizeAdjustment.ShrinkToFit,
            },
        };

        page.Accept(absorber);

        if (absorber.TextFragments.Count == 0)
        {
            testOutputHelper.WriteLine($"  NOT FOUND page={line.PageNumber}: \"{line.Text}\"");
            return;
        }

        foreach (TextFragment fragment in absorber.TextFragments)
        {
            fragment.Text = replacementText;
        }
    }

    /// <summary>
    /// Copies only the pages containing matched lines into a new standalone PDF (via PdfSharp, the
    /// same permission-bypassing Import-mode approach the other two POCs use), returning its path
    /// and a map from original page number to the new document's page number.
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
            $"{Path.GetFileNameWithoutExtension(sourcePath)}-aspose-extracted-pages.pdf");
        Directory.CreateDirectory(Path.GetDirectoryName(extractedPath)!);
        extracted.Save(extractedPath);

        return (extractedPath, pageNumberMap);
    }

    /// <summary>
    /// Builds a regex matching the given line's words in order with flexible whitespace between
    /// them, and either straight or curly quote characters wherever the line has an apostrophe or
    /// quote mark - same technique (and same reason) as WqFormITextSweepTests: the PDF's real text
    /// may not match PdfPig's normalized single-spaced, ASCII-quoted line text exactly.
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
