using System.Text.RegularExpressions;
using Path = System.IO.Path;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using iText.IO.Font.Constants;
using iText.Kernel.Colors;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Borders;
using iText.Layout.Element;
using iText.PdfCleanup;
using iText.PdfCleanup.Autosweep;
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
/// Trial of iText7's pdfSweep add-on as an alternative to the hand-rolled content-stream splicer
/// in <see cref="WqFormSpliceAndOverlayTests"/>. Removal here is delegated entirely to pdfSweep's
/// own text search/redaction engine (which extracts text the same robust way iText's normal text
/// extraction does, so it isn't limited to whole-operator matches or a single CID space per page
/// the way the hand-rolled version is) - no custom Tj/TJ/CMap parsing at all. Overlay uses iText's
/// own layout engine (Paragraph/Canvas) for automatic paragraph flow, rather than the manual wrap
/// measurement <see cref="WqFormParagraphOverlayTests.MeasureWrappedHeight"/> needed for PdfSharp.
/// iText7 and pdfSweep are AGPL/commercial - this is a local evaluation only, not for distribution.
/// </summary>
public class WqFormITextSweepTests(ITestOutputHelper testOutputHelper)
{
    [Theory]
    [MemberData(nameof(WqFormParagraphOverlayTests.SampleFiles), MemberType = typeof(WqFormParagraphOverlayTests))]
    public async Task WhenRealWqFormFile_ThenPdfSweepRemovesAndOverlaysText(string sourcePath)
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
            testOutputHelper.WriteLine($"No SpecialTermsWholeBlock match found in {filename} - nothing to sweep.");
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
            $"{Path.GetFileNameWithoutExtension(filename)}-itext-swept.pdf");

        SweepAndOverlay(sourcePath, outputPath, matchedLines, new WqFormParagraphOverlayTests.FillerTextCursor());

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote pdfSweep+overlay PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    /// <summary>
    /// Runs pdfSweep against every matched line's literal text (one regex-based strategy per
    /// line, escaped so punctuation isn't treated as a pattern) to genuinely remove it and paint a
    /// white redaction box, then overlays replacement text per page using iText's own layout engine.
    /// </summary>
    private static void SweepAndOverlay(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines,
        WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
        var reader = new PdfReader(sourcePath).SetUnethicalReading(true);
        using var pdfDocument = new PdfDocument(reader, new PdfWriter(outputPath));

        var strategy = new CompositeCleanupStrategy();

        foreach (var line in matchedLines)
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            strategy.Add(new RegexBasedCleanupStrategy(BuildWhitespaceTolerantPattern(line.Text)).SetRedactionColor(ColorConstants.WHITE));
        }

        PdfCleaner.AutoSweepCleanUp(pdfDocument, strategy);

        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);
        var linesByPage = matchedLines
            .GroupBy(line => line.PageNumber)
            .OrderBy(group => group.Key);

        foreach (var pageGroup in linesByPage)
        {
            var page = pdfDocument.GetPage(pageGroup.Key);
            OverlayNewParagraph(page, pageGroup, font, fillerCursor);
        }
    }

    /// <summary>
    /// Builds a regex matching the given line's words in order with flexible whitespace between
    /// them, and either straight or curly quote characters wherever the line has an apostrophe or
    /// quote mark - needed because iText's own text extraction can space words differently (e.g.
    /// justified text) and preserve typographic quotes as-is, while the line text this pattern is
    /// built from (PdfPig's) normalizes them to plain ASCII.
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

    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

    /// <summary>
    /// Draws one filler-text item per detected (a)/(b)/... marker in the given lines' bounding box,
    /// each as a hanging-indent paragraph, added sequentially to an iText <see cref="Canvas"/> so
    /// its layout engine flows each item after the previous one's actual rendered height - no
    /// manual wrap-height measurement needed, unlike the PdfSharp-based overlay tests.
    /// </summary>
    private static void OverlayNewParagraph(
        iText.Kernel.Pdf.PdfPage page, IEnumerable<DocumentLine> lines, PdfFont font, WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
        var lineList = lines.ToList();

        var words = lineList
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates)
            .Where(coordinates =>
                coordinates.Top >= 0 && coordinates.Right >= 0
                && coordinates.Bottom >= 0 && coordinates.Left >= 0)
            .ToList();

        const double padding = 2;
        var top = words.Max(coordinates => Math.Max(coordinates.Top, coordinates.Bottom)) + padding;
        var bottom = words.Min(coordinates => Math.Min(coordinates.Top, coordinates.Bottom)) - padding;
        var left = words.Min(coordinates => coordinates.Left) - padding;
        var right = words.Max(coordinates => coordinates.Right) + padding;

        // iText's coordinate space is native PDF space (origin bottom-left, Y-up) - same as
        // DocumentLine/word coordinates, so no Y-flip is needed here (unlike the PdfSharp-based
        // overlays, which draw in top-left/Y-down space). The area is given generous extra height
        // below the original block so the layout engine's own auto-flow has room to work without
        // clipping.
        const double extraHeight = 200;
        var area = new Rectangle((float)left, (float)(bottom - extraHeight), (float)(right - left), (float)(top - bottom + extraHeight));

        using var canvas = new Canvas(page, area);

        if (!lineList.Any(line => ItemMarkerRegex.IsMatch(line.Text)))
        {
            var text = fillerCursor.Next(WqFormParagraphOverlayTests.CombinedLength(lineList));
            canvas.Add(new Paragraph(text).SetFont(font).SetFontSize(10));
            return;
        }

        var itemGroups = WqFormParagraphOverlayTests.GroupLinesByMarker(lineList);

        var labelIndent = lineList
            .Where(line => ItemMarkerRegex.IsMatch(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= 0)
            .DefaultIfEmpty(left)
            .Min();

        var continuationLefts = lineList
            .Where(line => !ItemMarkerRegex.IsMatch(line.Text) && !string.IsNullOrWhiteSpace(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= labelIndent)
            .ToList();

        var continuationIndent = continuationLefts.Count > 0
            ? continuationLefts.Min()
            : labelIndent + 35;

        // A single Paragraph with SetFirstLineIndent (tried first) only pulls the label back on the
        // *first* line - the label and the first line of body text still share that one line, so
        // the body's first line starts wherever the label happens to end, not at the same fixed
        // column continuation lines snap to. A borderless two-column table gives the label and body
        // their own fixed columns instead, so the body text starts at the same x position on every
        // line, matching the layout the PdfSharp-based overlay tests draw by hand.
        var hangingIndent = (float)(continuationIndent - labelIndent);
        var bodyWidth = (float)(right - left) - hangingIndent;
        var table = new Table([hangingIndent, bodyWidth]).UseAllAvailableWidth();

        foreach (var itemLines in itemGroups)
        {
            var label = ItemMarkerRegex.Match(itemLines[0].Text).Groups[1].Value;
            var text = fillerCursor.Next(WqFormParagraphOverlayTests.CombinedLength(itemLines));

            table.AddCell(new Cell()
                .Add(new Paragraph($"({label})").SetFont(font).SetFontSize(10))
                .SetBorder(Border.NO_BORDER)
                .SetPadding(0)
                .SetPaddingBottom(6));

            table.AddCell(new Cell()
                .Add(new Paragraph(text).SetFont(font).SetFontSize(10))
                .SetBorder(Border.NO_BORDER)
                .SetPadding(0)
                .SetPaddingBottom(6));
        }

        canvas.Add(table);
    }
}
