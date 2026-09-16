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
/// replacement" question this trial exists to answer. Replaces per original PdfPig line rather
/// than per lettered clause (unlike the other two POCs), since that's the natural granularity for
/// this API. Aspose.PDF is fully commercial with no free tier - this trial runs unlicensed, so
/// output carries an evaluation watermark; not for distribution.
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

    /// <summary>
    /// For each matched line, finds its TextFragment via a whitespace/quote-tolerant regex and
    /// replaces its Text in place with a length-matched filler phrase, letting Aspose's own
    /// TextReplaceOptions handle any resulting length mismatch (shrinking the font to fit rather
    /// than us drawing a separate overlay).
    /// </summary>
    /// <remarks>
    /// Aspose.PDF's unlicensed evaluation mode caps any internal collection (including a
    /// document's own pages) at 4 elements - opening page 7 of these 24-26 page real files throws
    /// "At most 4 elements can be viewed in evaluation mode" outright, regardless of how many
    /// pages are actually touched. Worked around by first extracting only the pages that contain
    /// matched lines (via PdfSharp, which has no such limit) into a small standalone PDF, so
    /// Aspose only ever sees 1-2 pages - a real license would remove the need for this.
    /// </remarks>
    private void ReplaceLinesInPlace(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines,
        WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
        var (extractedPath, pageNumberMap) = ExtractMatchedPages(sourcePath, matchedLines);

        using var document = new Document(extractedPath);

        foreach (var line in matchedLines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
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
                continue;
            }

            var replacementText = fillerCursor.Next(line.Text.Length);

            foreach (TextFragment fragment in absorber.TextFragments)
            {
                fragment.Text = replacementText;
            }
        }

        document.Save(outputPath);
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
