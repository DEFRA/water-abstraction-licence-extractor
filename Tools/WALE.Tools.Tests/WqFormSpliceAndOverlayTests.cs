using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Pdf;
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

// Second example alongside WqFormParagraphOverlayTests. That test only ever covers the old text
// with a white rectangle - the underlying Tj/TJ operators are still in the content stream, so the
// "removed" text is still copy-pasteable/extractable. This one tries to actually DELETE those
// operators first (genuine removal), then overlays the new paragraph exactly as before.
//
// Deliberately "the simple splice": find each matched line's text-show operator by reconstructing
// its literal string content via regex and comparing against the line text PdfPig already gave
// us - no font-width/text-matrix interpretation, no glyph-ID decoding. That means it only works
// where the target text is stored as literal, readable strings (plain WinAnsi/TrueType fonts).
// Confirmed empirically this session: one of our two real sample files qualifies in full, the
// other's matched paragraph uses an Identity-H/Type0 font - the text is glyph-ID hex strings with
// no literal characters to reconstruct at all, so splicing correctly (and honestly) fails there,
// falling back to whiteout-only for that file. The full geometry-based interpreter needed to
// handle that case too is a separate, much bigger piece of work - not attempted here.
public class WqFormSpliceAndOverlayTests(ITestOutputHelper testOutputHelper)
{
    // The content stream is read/written as Latin1 - a 1:1 byte<->char mapping, needed so any
    // operator we don't touch round-trips back out as the exact original bytes. But PDF text
    // shown through a WinAnsiEncoding font is really Windows-1252, which assigns real characters
    // to the 0x80-0x9F byte range that Latin1 instead maps to C1 control codes - e.g. byte 0x92 is
    // U+2019 (curly apostrophe) in WinAnsi/CP1252 but U+0092 under Latin1. Confirmed empirically:
    // WQ__002671 clause (c)'s "operator's" encodes the apostrophe as a lone 0x92 byte, so without
    // this, the reconstructed literal text never matched PdfPig's correctly-decoded line text and
    // the line was wrongly reported non-spliceable.
    private static readonly Encoding Windows1252 = InitialiseWindows1252();

    private static Encoding InitialiseWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    private const string LoremIpsum =
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit. Sed do eiusmod tempor " +
        "incididunt ut labore et dolore magna aliqua. Ut enim ad minim veniam, quis nostrud " +
        "exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.";

    // One text-show operator: either a TJ array of interleaved strings/kerning numbers, or a
    // single-string Tj. Doesn't handle a literal, unescaped ']' inside a string (would end the
    // array match early) - a real limitation of a regex-based approach, acceptable for this POC.
    private static readonly Regex TjOperatorRegex = new(
        @"\[(?:[^\[\]]|\\.)*\]\s*TJ|\((?:[^()\\]|\\.)*\)\s*Tj",
        RegexOptions.Singleline);

    private static readonly Regex ParenGroupRegex = new(
        @"\((?<text>(?:[^()\\]|\\.)*)\)",
        RegexOptions.Singleline);

    [Theory]
    [MemberData(nameof(WqFormParagraphOverlayTests.SampleFiles), MemberType = typeof(WqFormParagraphOverlayTests))]
    public async Task WhenRealWqFormFile_ThenSpliceableTextIsGenuinelyRemoved(string sourcePath)
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
            testOutputHelper.WriteLine($"No SpecialTermsWholeBlock match found in {filename} - nothing to splice.");
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
            $"{Path.GetFileNameWithoutExtension(filename)}-spliced.pdf");

        SpliceAndOverlay(sourcePath, outputPath, matchedLines);

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote spliced+edited PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    private void SpliceAndOverlay(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines)
    {
        // Same permission-locked-source workaround as WqFormParagraphOverlayTests: copy pages
        // into a fresh PdfDocument rather than opening the original in Modify mode.
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
            var page = document.Pages[pageGroup.Key - 1];

            SpliceMatchedLinesFromContentStream(page, pageGroup);
            OverlayNewParagraph(page, pageGroup);
        }

        document.Save(outputPath);
    }

    private void SpliceMatchedLinesFromContentStream(PdfSharp.Pdf.PdfPage page, IEnumerable<DocumentLine> lines)
    {
        var contentDictionary = page.Contents.Elements.GetDictionary(0);
        var content = Encoding.Latin1.GetString(contentDictionary.Stream!.UnfilteredValue);

        foreach (var line in lines)
        {
            var (spliced, removed) = TryRemoveLineOperator(content, line.Text);
            content = spliced;

            testOutputHelper.WriteLine(
                removed
                    ? $"  SPLICED page={line.PageNumber}: \"{line.Text}\""
                    : $"  NOT SPLICEABLE (no literal text match - likely Identity-H/glyph-ID encoded) " +
                      $"page={line.PageNumber}: \"{line.Text}\"");
        }

        // UnfilteredValue is read-only - write back as an uncompressed stream instead (valid
        // PDF; just skips the FlateDecode step) by setting Value directly and dropping /Filter.
        contentDictionary.Stream.Value = Encoding.Latin1.GetBytes(content);
        contentDictionary.Elements.Remove("/Filter");
    }

    private static (string Content, bool Removed) TryRemoveLineOperator(string content, string lineText)
    {
        var target = NormalizeWhitespace(lineText);

        foreach (Match match in TjOperatorRegex.Matches(content))
        {
            var reconstructed = NormalizeWhitespace(ReconstructLiteralText(match.Value));

            // A short reconstructed fragment (e.g. an operator reconstructing to just "the" from
            // an unrelated part of the page) can trivially satisfy a plain Contains() check
            // against almost any real sentence - confirmed empirically as a real false-positive
            // source (WQ__003101's Identity-H-encoded target lines were wrongly reported as
            // spliced, via unrelated same-page WinAnsi text matching this way). Requiring the
            // match to cover a substantial proportion of the target line's length, not just any
            // containment, rules that out while still tolerating minor punctuation/whitespace
            // differences between PdfPig's line text and the operator's own literal content.
            if (reconstructed.Length == 0
                || reconstructed.Length < target.Length * 0.6)
            {
                continue;
            }

            if (target.Contains(reconstructed, StringComparison.OrdinalIgnoreCase)
                || reconstructed.Contains(target, StringComparison.OrdinalIgnoreCase))
            {
                return (content.Remove(match.Index, match.Length), true);
            }
        }

        return (content, false);
    }

    // Concatenates the literal parenthesized string runs within one Tj/TJ operator, ignoring the
    // kerning numbers between them - e.g. "[(If )6.4(the)8( )-6.4(mea)8(s)...]TJ" reconstructs to
    // "If the mea s...". Spaces inside the runs (like the one after "If") are preserved as-is
    // since PDF encodes them as literal space characters within a run.
    private static string ReconstructLiteralText(string operatorText)
    {
        var builder = new StringBuilder();

        foreach (Match match in ParenGroupRegex.Matches(operatorText))
        {
            builder.Append(UnescapePdfString(match.Groups["text"].Value));
        }

        return ToWindows1252(builder.ToString());
    }

    // builder's chars are still Latin1-per-byte (see the Windows1252 field comment above) -
    // convert back to the original bytes, then decode those through Windows-1252 to recover the
    // characters PdfPig's own DocumentLine.Text uses for the same bytes.
    private static string ToWindows1252(string latin1Text)
    {
        var bytes = new byte[latin1Text.Length];

        for (var i = 0; i < latin1Text.Length; i++)
        {
            bytes[i] = (byte)latin1Text[i];
        }

        return Windows1252.GetString(bytes);
    }

    private static string UnescapePdfString(string value) =>
        value.Replace("\\(", "(").Replace("\\)", ")").Replace("\\\\", "\\");

    // Both whitespace-collapsing and quote-folding, despite the name - kept as one step since both
    // sides of the comparison (target and reconstructed) always need both applied together. PdfPig's
    // line.Text normalizes typographic quotes down to their plain ASCII form (confirmed empirically:
    // WQ__002671's "operator's" comes through PdfPig as a straight U+0027, even though the content
    // stream's own byte - WinAnsi 0x92 - properly decodes to the curly U+2019). Without folding both
    // to the same form here, a correctly-decoded reconstructed string still fails to match.
    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(FoldTypographicQuotes(value), @"\s+", " ").Trim();

    private static string FoldTypographicQuotes(string value) =>
        value
            .Replace('‘', '\'').Replace('’', '\'')
            .Replace('“', '"').Replace('”', '"');

    private static void OverlayNewParagraph(PdfSharp.Pdf.PdfPage page, IEnumerable<DocumentLine> lines)
    {
        // Same word-level bounding-box computation as WqFormParagraphOverlayTests - see that
        // file for why line-level Top/Right/Bottom/Left and the "unknown coordinate" sentinel
        // filter are both necessary.
        var words = lines
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

        var pageHeight = page.Height.Point;
        var rect = new XRect(left, pageHeight - top, right - left, top - bottom);

        // Still whiteout the region even where splicing succeeded - splicing only removes the
        // matched glyphs, not any other marks (rule lines, background tint) that might be there,
        // and it's still needed outright wherever splicing failed.
        graphics.DrawRectangle(XBrushes.White, rect);
        DrawReplacementContent(graphics, rect, lines);
    }

    // Matches a lettered-list marker at the start of a line, e.g. "(a) " or "(e) " - see
    // WqFormParagraphOverlayTests for why the replacement mimics the original hanging-indent
    // list shape instead of one flat paragraph.
    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

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

        var labelIndent = lineList
            .Where(line => ItemMarkerRegex.IsMatch(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= 0)
            .DefaultIfEmpty(rect.X)
            .Min();

        // Only non-marker lines indented at least as much as the label count as real
        // continuation text - see WqFormParagraphOverlayTests for why (page furniture like a
        // page-footer line swept into the match otherwise drags this left of the label itself).
        var continuationLefts = lineList
            .Where(line => !ItemMarkerRegex.IsMatch(line.Text) && !string.IsNullOrWhiteSpace(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= labelIndent)
            .ToList();

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
