using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
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
using PdfDocumentOpenMode = PdfSharp.Pdf.IO.PdfDocumentOpenMode;

namespace WALE.Tools.Tests;

// POC: locate the "SpecialTermsWholeBlock" paragraph in a real WQ form PDF (via the existing,
// unchanged label-matching engine) and replace it in place with placeholder lorem ipsum text -
// redact (white rectangle) + overlay (draw new text), since no PDF format has a native "edit this
// paragraph" primitive. Handles a match spanning a page break by grouping the matched lines by
// page and drawing one whiteout+overlay per page - DocumentLine already carries a page-level
// bounding box (Top/Right/Bottom/Left) per line, so no new geometry needs computing beyond what
// the extraction pipeline already produces.
//
// Source PDFs live outside the repo (~/Downloads/WQ__*.pdf) - these are real regulatory
// documents, same convention as Wr51GroundTruthAccuracyTests keeping its truth-set PDFs external.
// Skips gracefully (empty theory data) if none are present on this machine.
public class WqFormParagraphOverlayTests(ITestOutputHelper testOutputHelper)
{
    // PdfSharp 6.x has no built-in cross-platform system-font access (it dropped the old
    // GDI-based resolution) - a resolver has to be supplied explicitly. Points straight at the
    // macOS system Arial for this POC rather than bundling a font file in the repo.
    private sealed class LocalFontResolver : IFontResolver
    {
        private const string FaceName = "Arial";
        private const string FontPath = "/System/Library/Fonts/Supplemental/Arial.ttf";

        public byte[] GetFont(string faceName) => File.ReadAllBytes(FontPath);

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic) =>
            new(FaceName);
    }

    static WqFormParagraphOverlayTests()
    {
        GlobalFontSettings.FontResolver ??= new LocalFontResolver();
    }

    private const string LoremIpsum =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor " +
        "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
        "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";

    public static IEnumerable<object[]> SampleFiles()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");

        if (!Directory.Exists(downloads))
        {
            yield break;
        }

        foreach (var file in Directory.GetFiles(downloads, "WQ__*.pdf"))
        {
            yield return [file];
        }
    }

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public async Task WhenRealWqFormFile_ThenSpecialTermsBlockIsReplacedWithLoremIpsum(string sourcePath)
    {
        var folder = Path.GetDirectoryName(sourcePath)!;
        var filename = Path.GetFileName(sourcePath);

        // LocalFileService concatenates folder + filename with no separator of its own - callers
        // are expected to include the trailing slash (same convention as
        // WRADI.ProcessFile.Cmd.AbstractionLicence/Program.cs's own pdfFolderPath handling).
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
            testOutputHelper.WriteLine($"No SpecialTermsWholeBlock match found in {filename} - nothing to overlay.");
            return;
        }

        var matchedLines = RemoveAccidentalOutliers(wholeBlockMatch.Text);

        testOutputHelper.WriteLine(
            $"Matched {matchedLines.Count} lines across pages " +
            $"{string.Join(",", matchedLines.Select(l => l.PageNumber).Distinct())}");

        foreach (var line in matchedLines)
        {
            testOutputHelper.WriteLine(
                $"  page={line.PageNumber} top={line.Top} right={line.Right} bottom={line.Bottom} " +
                $"left={line.Left} text=\"{line.Text}\"");
        }

        var comparisonFolder = Path.Combine(AppContext.BaseDirectory, "ComparisonOutput");
        Directory.CreateDirectory(comparisonFolder);

        var originalCopyPath = Path.Combine(
            comparisonFolder,
            $"{Path.GetFileNameWithoutExtension(filename)}-original.pdf");
        File.Copy(sourcePath, originalCopyPath, overwrite: true);

        var outputPath = Path.Combine(
            comparisonFolder,
            $"{Path.GetFileNameWithoutExtension(filename)}-edited.pdf");

        ReplaceMatchedBlockWithLoremIpsum(sourcePath, outputPath, matchedLines);

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote edited PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    // A match spanning a page break can sweep up page furniture (headers/footers) sitting
    // between the last real content line and the page boundary - confirmed as a real bug: a page
    // footer ("Permit 002671 number 7") got included, and its far-outlier Y-position corrupted
    // the whole block's bounding box, which both mis-positioned the (a) label (dragged left) and
    // erased the footer itself under the whiteout rectangle (dragged the box's bottom edge down
    // to cover it). Detected generically per page rather than by matching the literal footer
    // text: sort lines top-to-bottom, keep the leading contiguous run, stop at the first gap much
    // bigger than a normal line-to-line gap (confirmed ~7-13pt in this document; the footer's gap
    // was ~42pt).
    internal static List<DocumentLine> RemoveAccidentalOutliers(IEnumerable<DocumentLine> lines)
    {
        const double maxNormalGap = 20;

        return lines
            .GroupBy(line => line.PageNumber)
            .SelectMany(pageGroup =>
            {
                var ordered = pageGroup
                    .Where(line => !string.IsNullOrWhiteSpace(line.Text))
                    .OrderByDescending(line => line.Top)
                    .ToList();

                if (ordered.Count == 0)
                {
                    return ordered;
                }

                var kept = new List<DocumentLine> { ordered[0] };

                foreach (var line in ordered.Skip(1))
                {
                    if (kept[^1].Bottom - line.Top > maxNormalGap)
                    {
                        break;
                    }

                    kept.Add(line);
                }

                return kept;
            })
            .ToList();
    }

    private static void ReplaceMatchedBlockWithLoremIpsum(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines)
    {
        // These are real government permit documents with the PDF "no editing" permission flag
        // set (Modify mode throws PdfReaderException requiring the owner password, which we
        // don't have and shouldn't be trying to guess) - Import mode is read-only and ignores
        // that restriction, so instead of modifying the source file we copy its pages into a
        // fresh PdfDocument (which carries no such restriction) and draw on the copies.
        using var sourceDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        using var document = new PdfSharp.Pdf.PdfDocument();

        foreach (var sourcePage in sourceDocument.Pages)
        {
            document.AddPage(sourcePage);
        }

        var linesByPage = matchedLines
            .GroupBy(line => line.PageNumber)
            .OrderBy(group => group.Key);

        foreach (var pageGroup in linesByPage)
        {
            // PageNumber is 1-based (matches the source doc's own page numbering); PdfSharp's
            // page collection is 0-indexed.
            var page = document.Pages[pageGroup.Key - 1];

            // DocumentLine.Top/Right/Bottom/Left don't reliably span the line's full rendered
            // width (confirmed empirically against these real files - Right consistently landed
            // well short of where the line's text actually ends on the page), so the bounding box
            // is built from the individual words' own coordinates instead, which do match the
            // real per-word bounding boxes PdfPig reports.
            // Some words carry the "not known" coordinate sentinel (-1 on every side) rather than
            // a real position - confirmed empirically (a handful of words on one page of one of
            // these files). Left in, a single sentinel word drags the whole page's bounding box
            // out to roughly the full page size via Math.Min/Max. Real coordinates are never
            // negative, so filtering on that is enough to drop them.
            var words = pageGroup
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

            using var graphics = XGraphics.FromPdfPage(page);

            // DocumentLine/word coordinates are native PDF space (origin bottom-left, Y-up);
            // XGraphics draws in top-left/Y-down space, so Y needs flipping against page height.
            var pageHeight = page.Height.Point;
            var rect = new XRect(left, pageHeight - top, right - left, top - bottom);

            graphics.DrawRectangle(XBrushes.White, rect);
            DrawReplacementContent(graphics, rect, pageGroup);
        }

        document.Save(outputPath);
    }

    // Matches a lettered-list marker at the start of a line, e.g. "(a) " or "(e) " - this is how
    // the original clauses are actually laid out (confirmed against the real files), so the
    // replacement should mimic the same hanging-indent list shape rather than one flat paragraph.
    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

    // Lays out one lorem-ipsum "item" per detected (a)/(b)/... marker in this page's matched
    // lines, each as its own hanging-indent paragraph (label at the block's left edge,
    // wrapped body text starting at the original continuation indent) - rather than one
    // continuous block of prose, which doesn't resemble what was actually there. Falls back to a
    // single plain block if no markers are found (e.g. a match that's pure prose, no lettered list).
    private static void DrawReplacementContent(XGraphics graphics, XRect rect, IEnumerable<DocumentLine> lines)
    {
        var lineList = lines.ToList();
        var labels = lineList
            .Select(line => ItemMarkerRegex.Match(line.Text))
            .Where(match => match.Success)
            .Select(match => match.Groups[1].Value)
            .ToList();

        var font = new XFont("Arial", 10);

        if (labels.Count == 0)
        {
            var textFormatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };
            textFormatter.DrawString(LoremIpsum, font, XBrushes.Black, rect);
            return;
        }

        // Label indent is the smallest left position among the marker lines themselves - not
        // rect.X, which is the padded whole-block bound and can be dragged left by unrelated
        // page furniture swept into the match (see below).
        var labelIndent = lineList
            .Where(line => ItemMarkerRegex.IsMatch(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= 0)
            .DefaultIfEmpty(rect.X)
            .Min();

        // Continuation indent (where wrapped body text starts, under the label rather than
        // under its own left margin) is the smallest left position among non-marker lines - but
        // ONLY those indented at least as much as the label itself. Confirmed empirically as a
        // real bug otherwise: a match spanning a page break can sweep up page furniture in
        // between (here, a page footer "Permit 002671 number 7" at Left=79.32, well left of the
        // label's own 111.36) which isn't blank but also isn't real continuation text - left
        // unfiltered, it drags the indent left of the label and the two overlap when drawn.
        var continuationLefts = lineList
            .Where(line => !ItemMarkerRegex.IsMatch(line.Text) && !string.IsNullOrWhiteSpace(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= labelIndent)
            .ToList();

        // Fallback matters when an item is a single line (the marker line itself, with no
        // continuation lines to measure) - too small a fixed offset here draws the label
        // overlapping the body text at 10pt Arial.
        var continuationIndent = continuationLefts.Count > 0
            ? continuationLefts.Min()
            : labelIndent + 35;

        var itemHeight = rect.Height / labels.Count;
        var itemFormatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };

        for (var index = 0; index < labels.Count; index++)
        {
            var itemTop = rect.Y + (itemHeight * index);
            var labelRect = new XRect(rect.X, itemTop, continuationIndent - rect.X, itemHeight);
            var bodyRect = new XRect(continuationIndent, itemTop, rect.Right - continuationIndent, itemHeight);

            graphics.DrawString($"({labels[index]})", font, XBrushes.Black, labelRect, XStringFormats.TopLeft);
            itemFormatter.DrawString(LoremIpsum, font, XBrushes.Black, bodyRect);
        }
    }
}
