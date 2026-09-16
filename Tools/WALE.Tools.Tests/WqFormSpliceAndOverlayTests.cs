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

/// <summary>
/// Second example alongside <see cref="WqFormParagraphOverlayTests"/>. That test only ever covers
/// the old text with a white rectangle, leaving the underlying Tj/TJ operators (and so the
/// "removed" text) still present and extractable. This one deletes those operators first, then
/// overlays the new paragraph the same way. Handles both literal WinAnsi/TrueType text and
/// Identity-H/Type0 glyph IDs (resolved through the font's own embedded /ToUnicode CMap). Cannot
/// handle a Type0 font with no /ToUnicode entry at all.
/// </summary>
public class WqFormSpliceAndOverlayTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>
    /// Windows-1252 encoding, used to correctly decode WinAnsi text runs pulled out of a content
    /// stream that was otherwise read as Latin1 (a 1:1 byte&lt;-&gt;char mapping, needed so any
    /// untouched operator round-trips back out as the exact original bytes). Latin1 alone would
    /// map bytes like 0x92 (a curly apostrophe under WinAnsi) to the wrong character.
    /// </summary>
    private static readonly Encoding Windows1252 = InitialiseWindows1252();

    private static Encoding InitialiseWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    /// <summary>One text-show operator: a TJ array, or a single-string Tj (literal or hex).</summary>
    private static readonly Regex TjOperatorRegex = new(
        @"\[(?:[^\[\]]|\\.)*\]\s*TJ|\((?:[^()\\]|\\.)*\)\s*Tj|<[0-9A-Fa-f\s]*>\s*Tj",
        RegexOptions.Singleline);

    /// <summary>A literal "(...)" run or a hex "&lt;...&gt;" run within one text-show operator.</summary>
    private static readonly Regex StringRunRegex = new(
        @"\((?<lit>(?:[^()\\]|\\.)*)\)|<(?<hex>[0-9A-Fa-f\s]*)>",
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

        SpliceAndOverlay(sourcePath, outputPath, matchedLines, new WqFormParagraphOverlayTests.FillerTextCursor());

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote spliced+edited PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    /// <summary>
    /// Copies the source PDF's pages into a fresh document (bypassing its edit-permission
    /// restriction), then for each page containing matched lines splices out their text-show
    /// operators and overlays replacement text.
    /// </summary>
    private void SpliceAndOverlay(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines,
        WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
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
            OverlayNewParagraph(page, pageGroup, fillerCursor);
        }

        document.Save(outputPath);
    }

    /// <summary>
    /// Removes each matched line's text-show operator(s) from the page's content stream, logging
    /// whether each line was found and spliced or left in place.
    /// </summary>
    private void SpliceMatchedLinesFromContentStream(PdfSharp.Pdf.PdfPage page, IEnumerable<DocumentLine> lines)
    {
        var contentDictionary = page.Contents.Elements.GetDictionary(0);
        var content = Encoding.Latin1.GetString(contentDictionary.Stream!.UnfilteredValue);
        var toUnicodeMap = BuildToUnicodeMap(page);

        foreach (var line in lines)
        {
            var (spliced, removed) = TryRemoveLineOperator(content, line.Text, toUnicodeMap);
            content = spliced;

            testOutputHelper.WriteLine(
                removed
                    ? $"  SPLICED page={line.PageNumber}: \"{line.Text}\""
                    : $"  NOT SPLICEABLE (no literal or ToUnicode-mapped text match) " +
                      $"page={line.PageNumber}: \"{line.Text}\"");
        }

        // UnfilteredValue is read-only - write back as an uncompressed stream instead (valid
        // PDF; just skips the FlateDecode step) by setting Value directly and dropping /Filter.
        contentDictionary.Stream.Value = Encoding.Latin1.GetBytes(content);
        contentDictionary.Elements.Remove("/Filter");
    }

    private const int MaxOperatorsPerLine = 10;

    /// <summary>
    /// Finds the run of consecutive text-show operators whose reconstructed text matches
    /// <paramref name="lineText"/> and removes them from <paramref name="content"/>. A line's text
    /// can be split across several operators (e.g. a lettered-list marker, its following space, and
    /// the paragraph body each drawn separately), so this tries every possible starting operator
    /// and greedily accumulates forward - requiring the accumulation to stay an exact prefix of the
    /// target throughout - until the whole line is covered, then removes the entire matched run.
    /// </summary>
    private static (string Content, bool Removed) TryRemoveLineOperator(
        string content, string lineText, IReadOnlyDictionary<int, string> toUnicodeMap)
    {
        var target = NormalizeWhitespace(lineText);
        var matches = TjOperatorRegex.Matches(content).Cast<Match>().ToList();
        var reconstructedPerOperator = matches
            .Select(match => ReconstructLiteralText(match.Value, toUnicodeMap))
            .ToList();

        for (var startIndex = 0; startIndex < matches.Count; startIndex++)
        {
            var accumulated = new StringBuilder();
            var matchEndIndex = -1;

            for (var index = startIndex;
                 index < matches.Count && index - startIndex < MaxOperatorsPerLine;
                 index++)
            {
                accumulated.Append(reconstructedPerOperator[index]);
                var normalizedAccumulated = NormalizeWhitespace(accumulated.ToString());

                if (normalizedAccumulated.Length == 0)
                {
                    continue;
                }

                if (normalizedAccumulated.Length >= target.Length)
                {
                    if (normalizedAccumulated.Length <= target.Length * 1.5
                        && normalizedAccumulated.StartsWith(target, StringComparison.OrdinalIgnoreCase))
                    {
                        matchEndIndex = index;
                    }

                    break;
                }

                if (!target.StartsWith(normalizedAccumulated, StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
            }

            if (matchEndIndex < 0)
            {
                continue;
            }

            var removeStart = matches[startIndex].Index;
            var removeLength = matches[matchEndIndex].Index + matches[matchEndIndex].Length - removeStart;

            return (content.Remove(removeStart, removeLength), true);
        }

        return (content, false);
    }

    /// <summary>
    /// Concatenates the literal and hex string runs within one Tj/TJ operator, in order, ignoring
    /// the kerning numbers between them. A literal run is decoded as WinAnsi text; a hex run is a
    /// string of Identity-H glyph IDs resolved to characters via <paramref name="toUnicodeMap"/>.
    /// </summary>
    private static string ReconstructLiteralText(string operatorText, IReadOnlyDictionary<int, string> toUnicodeMap)
    {
        var builder = new StringBuilder();

        foreach (Match match in StringRunRegex.Matches(operatorText))
        {
            if (match.Groups["lit"].Success)
            {
                builder.Append(ToWindows1252(UnescapePdfString(match.Groups["lit"].Value)));
            }
            else if (match.Groups["hex"].Success)
            {
                builder.Append(DecodeHexGlyphs(match.Groups["hex"].Value, toUnicodeMap));
            }
        }

        return builder.ToString();
    }

    /// <summary>Decodes a hex string of fixed 2-byte glyph IDs to characters via the given CMap.</summary>
    private static string DecodeHexGlyphs(string hex, IReadOnlyDictionary<int, string> toUnicodeMap)
    {
        var cleaned = Regex.Replace(hex, @"\s+", string.Empty);
        var builder = new StringBuilder();

        for (var i = 0; i + 4 <= cleaned.Length; i += 4)
        {
            var cid = Convert.ToInt32(cleaned.Substring(i, 4), 16);

            if (toUnicodeMap.TryGetValue(cid, out var mapped))
            {
                builder.Append(mapped);
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Reads every Type0 font's embedded /ToUnicode CMap on the given page and merges them into
    /// one CID-to-character lookup. Assumes CIDs don't collide across fonts sharing a page.
    /// </summary>
    private static Dictionary<int, string> BuildToUnicodeMap(PdfSharp.Pdf.PdfPage page)
    {
        var map = new Dictionary<int, string>();
        var fontDictionary = page.Resources.Elements.GetDictionary("/Font");

        if (fontDictionary == null)
        {
            return map;
        }

        foreach (var fontName in fontDictionary.Elements.Keys.ToList())
        {
            var font = fontDictionary.Elements.GetDictionary(fontName);

            if (font == null || font.Elements.GetName("/Subtype") != "/Type0")
            {
                continue;
            }

            var toUnicode = font.Elements.GetDictionary("/ToUnicode");

            if (toUnicode?.Stream == null)
            {
                continue;
            }

            var cmapText = Encoding.Latin1.GetString(toUnicode.Stream.UnfilteredValue);

            foreach (var (cid, character) in ParseToUnicodeCMap(cmapText))
            {
                map[cid] = character;
            }
        }

        return map;
    }

    private static readonly Regex BfCharBlockRegex =
        new(@"beginbfchar(?<body>.*?)endbfchar", RegexOptions.Singleline);

    private static readonly Regex BfRangeBlockRegex =
        new(@"beginbfrange(?<body>.*?)endbfrange", RegexOptions.Singleline);

    private static readonly Regex BfCharEntryRegex = new(
        @"<(?<src>[0-9A-Fa-f]+)>\s*<(?<dst>[0-9A-Fa-f]+)>",
        RegexOptions.Singleline);

    private static readonly Regex BfRangeEntryRegex = new(
        @"<(?<lo>[0-9A-Fa-f]+)>\s*<(?<hi>[0-9A-Fa-f]+)>\s*(?:<(?<dst>[0-9A-Fa-f]+)>|\[(?<dstArray>(?:\s*<[0-9A-Fa-f]+>)+)\s*\])",
        RegexOptions.Singleline);

    private static readonly Regex HexTokenRegex = new(@"<([0-9A-Fa-f]+)>");

    /// <summary>
    /// Parses the "bfchar" (one-CID-to-one-string) and "bfrange" (a contiguous CID span mapped to
    /// either a base codepoint or an explicit array of strings) constructs of a /ToUnicode CMap.
    /// </summary>
    private static IEnumerable<(int Cid, string Character)> ParseToUnicodeCMap(string cmapText)
    {
        foreach (Match block in BfCharBlockRegex.Matches(cmapText))
        {
            foreach (Match entry in BfCharEntryRegex.Matches(block.Groups["body"].Value))
            {
                yield return (
                    Convert.ToInt32(entry.Groups["src"].Value, 16),
                    HexToUtf16String(entry.Groups["dst"].Value));
            }
        }

        foreach (Match block in BfRangeBlockRegex.Matches(cmapText))
        {
            foreach (Match entry in BfRangeEntryRegex.Matches(block.Groups["body"].Value))
            {
                var lo = Convert.ToInt32(entry.Groups["lo"].Value, 16);
                var hi = Convert.ToInt32(entry.Groups["hi"].Value, 16);

                if (entry.Groups["dstArray"].Success)
                {
                    var values = HexTokenRegex.Matches(entry.Groups["dstArray"].Value)
                        .Select(tokenMatch => tokenMatch.Groups[1].Value)
                        .ToList();

                    for (var cid = lo; cid <= hi && cid - lo < values.Count; cid++)
                    {
                        yield return (cid, HexToUtf16String(values[cid - lo]));
                    }
                }
                else
                {
                    var dstHex = entry.Groups["dst"].Value;

                    for (var cid = lo; cid <= hi; cid++)
                    {
                        yield return (cid, OffsetHexCodepoint(dstHex, cid - lo));
                    }
                }
            }
        }
    }

    /// <summary>Decodes a UTF-16BE hex string to its characters.</summary>
    private static string HexToUtf16String(string hex) =>
        Encoding.BigEndianUnicode.GetString(Convert.FromHexString(hex));

    /// <summary>Adds <paramref name="offset"/> to a UTF-16BE hex codepoint and decodes the result.</summary>
    private static string OffsetHexCodepoint(string dstHex, int offset)
    {
        var value = Convert.ToInt64(dstHex, 16) + offset;
        return HexToUtf16String(value.ToString($"X{dstHex.Length}"));
    }

    /// <summary>
    /// Converts a string whose characters are each one Latin1-decoded byte back into those bytes,
    /// then decodes them as Windows-1252 to recover the characters WinAnsi text actually represents.
    /// </summary>
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

    /// <summary>
    /// Collapses whitespace and folds typographic quotes to their plain ASCII form, so text
    /// reconstructed from the content stream can be compared against PdfPig's own line text (which
    /// normalizes quotes the same way).
    /// </summary>
    private static string NormalizeWhitespace(string value) =>
        Regex.Replace(FoldTypographicQuotes(value), @"\s+", " ").Trim();

    private static string FoldTypographicQuotes(string value) =>
        value
            .Replace('‘', '\'').Replace('’', '\'')
            .Replace('“', '"').Replace('”', '"');

    /// <summary>
    /// Draws a white rectangle over the given lines' bounding box and overlays replacement text.
    /// </summary>
    private static void OverlayNewParagraph(
        PdfSharp.Pdf.PdfPage page,
        IEnumerable<DocumentLine> lines,
        WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
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
        // matched glyphs, not any other marks that might be there, and it's needed outright
        // wherever splicing failed.
        graphics.DrawRectangle(XBrushes.White, rect);
        DrawReplacementContent(graphics, rect, lines, fillerCursor);
    }

    /// <summary>Matches a lettered-list marker at the start of a line, e.g. "(a) " or "(e) ".</summary>
    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

    /// <summary>
    /// Draws one filler-text item per detected (a)/(b)/... marker in the given lines, each as its
    /// own hanging-indent paragraph sized to its own measured wrapped height. Falls back to a
    /// single plain block if no markers are found.
    /// </summary>
    private static void DrawReplacementContent(
        XGraphics graphics,
        XRect rect,
        IEnumerable<DocumentLine> lines,
        WqFormParagraphOverlayTests.FillerTextCursor fillerCursor)
    {
        var lineList = lines.ToList();
        var font = new XFont("Arial", 10);

        if (!lineList.Any(line => ItemMarkerRegex.IsMatch(line.Text)))
        {
            var textFormatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };
            textFormatter.DrawString(
                fillerCursor.Next(WqFormParagraphOverlayTests.CombinedLength(lineList)),
                font,
                XBrushes.Black,
                rect);
            return;
        }

        var itemGroups = WqFormParagraphOverlayTests.GroupLinesByMarker(lineList);

        var labelIndent = lineList
            .Where(line => ItemMarkerRegex.IsMatch(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= 0)
            .DefaultIfEmpty(rect.X)
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

        var bodyWidth = rect.Right - continuationIndent;
        var itemTexts = itemGroups
            .Select(itemLines => fillerCursor.Next(WqFormParagraphOverlayTests.CombinedLength(itemLines)))
            .ToList();
        var itemHeights = itemTexts
            .Select(text => WqFormParagraphOverlayTests.MeasureWrappedHeight(graphics, text, font, bodyWidth))
            .ToList();
        var itemFormatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };
        var itemTop = rect.Y;

        for (var index = 0; index < itemGroups.Count; index++)
        {
            var itemLines = itemGroups[index];
            var label = ItemMarkerRegex.Match(itemLines[0].Text).Groups[1].Value;
            var itemHeight = itemHeights[index];
            var labelRect = new XRect(rect.X, itemTop, continuationIndent - rect.X, itemHeight);
            var bodyRect = new XRect(continuationIndent, itemTop, bodyWidth, itemHeight);

            graphics.DrawString($"({label})", font, XBrushes.Black, labelRect, XStringFormats.TopLeft);
            itemFormatter.DrawString(itemTexts[index], font, XBrushes.Black, bodyRect);
            itemTop += itemHeight + WqFormParagraphOverlayTests.InterItemGap;
        }
    }
}
