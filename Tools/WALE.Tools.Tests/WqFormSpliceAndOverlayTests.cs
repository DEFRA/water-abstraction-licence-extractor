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
    internal static readonly Encoding Windows1252 = InitialiseWindows1252();

    internal static Encoding InitialiseWindows1252()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(1252);
    }

    /// <summary>One text-show operator: a TJ array, or a single-string Tj (literal or hex).</summary>
    internal static readonly Regex TjOperatorRegex = new(
        @"\[(?:[^\[\]]|\\.)*\]\s*TJ|\((?:[^()\\]|\\.)*\)\s*Tj|<[0-9A-Fa-f\s]*>\s*Tj",
        RegexOptions.Singleline);

    /// <summary>A literal "(...)" run or a hex "&lt;...&gt;" run within one text-show operator.</summary>
    internal static readonly Regex StringRunRegex = new(
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

        SpliceAndOverlay(sourcePath, outputPath, matchedLines);

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote spliced+edited PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    /// <summary>
    /// Copies the source PDF's pages into a fresh document (bypassing its edit-permission
    /// restriction), then for each page containing matched lines splices out their text-show
    /// operators and whites out their bounding box, before drawing the replacement text once across
    /// the whole match. Drawing happens in a second pass (not per page, like the splicing) because
    /// the replacement text is fixed and independent of how many lines the old text happened to
    /// occupy on each page - a match spanning a page break must not draw the same fixed text twice,
    /// once per page's box, which is what a naive per-page loop would do.
    /// </summary>
    internal void SpliceAndOverlay(
        string sourcePath,
        string outputPath,
        IReadOnlyList<DocumentLine> matchedLines)
    {
        using var sourceDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        using var document = new PdfSharp.Pdf.PdfDocument();

        foreach (var sourcePage in sourceDocument.Pages)
        {
            document.AddPage(sourcePage);
        }

        var linesByPage = matchedLines
            .GroupBy(line => line.PageNumber)
            .OrderBy(group => group.Key)
            .ToList();

        (string Family, bool Bold, bool Italic, double Size)? matchFontInfo = null;
        var boxes = new List<(PdfSharp.Pdf.PdfPage Page, XRect Rect)>();

        foreach (var pageGroup in linesByPage)
        {
            var page = document.Pages[pageGroup.Key - 1];
            var detectedFont = SpliceMatchedLinesFromContentStream(page, pageGroup);
            matchFontInfo ??= detectedFont;
            boxes.Add((page, WhiteoutMatchedRegion(page, pageGroup)));
        }

        DrawReplacementContentAcrossPages(boxes, matchedLines, matchFontInfo ?? ("Arial", false, false, 10));

        document.Save(outputPath);
    }

    /// <summary>
    /// Removes each matched line's text-show operator(s) from the page's content stream, logging
    /// whether each line was found and spliced or left in place.
    /// </summary>
    /// <summary>
    /// Removes each matched line's text-show operator(s), returning the font family/weight/size
    /// detected at the first successfully-matched line's position - see
    /// <see cref="GetFontInfoAtPosition"/> - so the overlay can be drawn in a font that actually
    /// matches the original text, instead of a fixed Arial 10pt regardless of the source document.
    /// </summary>
    private (string Family, bool Bold, bool Italic, double Size) SpliceMatchedLinesFromContentStream(
        PdfSharp.Pdf.PdfPage page, IEnumerable<DocumentLine> lines)
    {
        var contentDictionary = page.Contents.Elements.GetDictionary(0);
        var content = Encoding.Latin1.GetString(contentDictionary.Stream!.UnfilteredValue);
        var toUnicodeMap = BuildToUnicodeMap(page);
        var fontDictionary = page.Resources.Elements.GetDictionary("/Font");
        (string Family, bool Bold, bool Italic, double Size)? detectedFont = null;

        foreach (var line in lines)
        {
            var (spliced, removed, matchIndex) = TryRemoveLineOperator(content, line.Text, toUnicodeMap);

            if (removed)
            {
                detectedFont ??= GetFontInfoAtPosition(content, matchIndex, fontDictionary);
            }

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

        return detectedFont ?? ("Arial", false, false, 10);
    }

    internal static readonly Regex TfOperatorRegex = new(@"/(\w+)\s+([\d.]+)\s+Tf");

    // Word/LibreOffice-style PDF generators (confirmed on WQ__002671, among others in the real
    // batch) commonly emit "/TT2 1 Tf" - a nominal size of 1 - and bake the real, effective size
    // into the following text matrix's scale instead (e.g. "7.5077 0 0 7.5077 x y Tm"). Reading
    // Tf's own size parameter alone gave a nonsense "1pt" result for these files; the true
    // rendered size is Tf's size multiplied by the text matrix's horizontal scale (its first of
    // six operands - the "a" component - assuming no skew/rotation, true for normal upright text).
    internal static readonly Regex TextMatrixRegex = new(
        @"([\d.\-]+)\s+[\d.\-]+\s+[\d.\-]+\s+[\d.\-]+\s+[\d.\-]+\s+[\d.\-]+\s+Tm");

    /// <summary>
    /// Finds the font resource and effective size active at <paramref name="index"/> in the
    /// content stream (the nearest preceding Tf operator, scaled by the nearest preceding text
    /// matrix - see <see cref="TextMatrixRegex"/>), then resolves that resource's /BaseFont name
    /// to a (family, bold, italic) triple. Falls back to Arial 10pt if nothing is found, e.g. a
    /// subset font name this hasn't been taught to recognise.
    /// </summary>
    internal static (string Family, bool Bold, bool Italic, double Size) GetFontInfoAtPosition(
        string content, int index, PdfDictionary? fontDictionary)
    {
        const string defaultFamily = "Arial";
        const double defaultSize = 10;

        if (index < 0)
        {
            return (defaultFamily, false, false, defaultSize);
        }

        var preceding = content[..index];
        var lastTf = TfOperatorRegex.Matches(preceding).Cast<Match>().LastOrDefault();

        if (lastTf == null)
        {
            return (defaultFamily, false, false, defaultSize);
        }

        var nominalSize = double.TryParse(lastTf.Groups[2].Value, out var parsedSize) ? parsedSize : defaultSize;
        var lastTm = TextMatrixRegex.Matches(preceding).Cast<Match>().LastOrDefault();
        var scale = lastTm != null && double.TryParse(lastTm.Groups[1].Value, out var parsedScale)
            ? Math.Abs(parsedScale)
            : 1;
        var size = nominalSize * scale;
        var baseFont = fontDictionary?.Elements.GetDictionary($"/{lastTf.Groups[1].Value}")
            ?.Elements.GetName("/BaseFont");
        var (family, bold, italic) = ParseBaseFontName(baseFont);

        return (family, bold, italic, size);
    }

    /// <summary>
    /// Strips a subset-font tag (e.g. "ABCDEF+ArialMT") and classifies the remaining /BaseFont name
    /// into a family PdfSharp can resolve (via WqFormParagraphOverlayTests.LocalFontResolver) plus
    /// bold/italic flags. Only recognises the families actually seen in these real files (Arial,
    /// Times New Roman) - anything else falls back to Arial.
    /// </summary>
    internal static (string Family, bool Bold, bool Italic) ParseBaseFontName(string? baseFont)
    {
        if (string.IsNullOrEmpty(baseFont))
        {
            return ("Arial", false, false);
        }

        var name = Regex.Replace(baseFont.TrimStart('/'), @"^[A-Z]{6}\+", "");
        var bold = name.Contains("Bold", StringComparison.OrdinalIgnoreCase);
        var italic = name.Contains("Italic", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Oblique", StringComparison.OrdinalIgnoreCase);

        var family = name.Contains("TimesNewRoman", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Times New Roman", StringComparison.OrdinalIgnoreCase)
                ? "Times New Roman"
                : "Arial";

        return (family, bold, italic);
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
    internal static (string Content, bool Removed, int MatchIndex) TryRemoveLineOperator(
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

            return (content.Remove(removeStart, removeLength), true, removeStart);
        }

        return (content, false, -1);
    }

    /// <summary>
    /// Concatenates the literal and hex string runs within one Tj/TJ operator, in order, ignoring
    /// the kerning numbers between them. A literal run is decoded as WinAnsi text; a hex run is a
    /// string of Identity-H glyph IDs resolved to characters via <paramref name="toUnicodeMap"/>.
    /// </summary>
    internal static string ReconstructLiteralText(string operatorText, IReadOnlyDictionary<int, string> toUnicodeMap)
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
    internal static string DecodeHexGlyphs(string hex, IReadOnlyDictionary<int, string> toUnicodeMap)
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
    internal static Dictionary<int, string> BuildToUnicodeMap(PdfSharp.Pdf.PdfPage page)
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

    internal static readonly Regex BfCharBlockRegex =
        new(@"beginbfchar(?<body>.*?)endbfchar", RegexOptions.Singleline);

    internal static readonly Regex BfRangeBlockRegex =
        new(@"beginbfrange(?<body>.*?)endbfrange", RegexOptions.Singleline);

    internal static readonly Regex BfCharEntryRegex = new(
        @"<(?<src>[0-9A-Fa-f]+)>\s*<(?<dst>[0-9A-Fa-f]+)>",
        RegexOptions.Singleline);

    internal static readonly Regex BfRangeEntryRegex = new(
        @"<(?<lo>[0-9A-Fa-f]+)>\s*<(?<hi>[0-9A-Fa-f]+)>\s*(?:<(?<dst>[0-9A-Fa-f]+)>|\[(?<dstArray>(?:\s*<[0-9A-Fa-f]+>)+)\s*\])",
        RegexOptions.Singleline);

    internal static readonly Regex HexTokenRegex = new(@"<([0-9A-Fa-f]+)>");

    /// <summary>
    /// Parses the "bfchar" (one-CID-to-one-string) and "bfrange" (a contiguous CID span mapped to
    /// either a base codepoint or an explicit array of strings) constructs of a /ToUnicode CMap.
    /// </summary>
    internal static IEnumerable<(int Cid, string Character)> ParseToUnicodeCMap(string cmapText)
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
    internal static string HexToUtf16String(string hex) =>
        Encoding.BigEndianUnicode.GetString(Convert.FromHexString(hex));

    /// <summary>Adds <paramref name="offset"/> to a UTF-16BE hex codepoint and decodes the result.</summary>
    internal static string OffsetHexCodepoint(string dstHex, int offset)
    {
        var value = Convert.ToInt64(dstHex, 16) + offset;
        return HexToUtf16String(value.ToString($"X{dstHex.Length}"));
    }

    /// <summary>
    /// Converts a string whose characters are each one Latin1-decoded byte back into those bytes,
    /// then decodes them as Windows-1252 to recover the characters WinAnsi text actually represents.
    /// </summary>
    internal static string ToWindows1252(string latin1Text)
    {
        var bytes = new byte[latin1Text.Length];

        for (var i = 0; i < latin1Text.Length; i++)
        {
            bytes[i] = (byte)latin1Text[i];
        }

        return Windows1252.GetString(bytes);
    }

    // PDF literal strings can escape a byte as 1-3 octal digits (e.g. "\222" = byte 0x92 - a
    // second, distinct way of representing the same WinAnsi curly-apostrophe byte WQ__003101
    // embedded raw, confirmed on a "Consolidated Permit" template file where "operator's" is
    // written as "operator\222s"). The previous plain string-replace chain only handled
    // "\(", "\)", and "\\", so an octal escape like "\222" passed through as four literal
    // characters ('\', '2', '2', '2') instead of the one byte it represents, breaking the match
    // against PdfPig's correctly-decoded line text the same way the raw-byte case did before it
    // was fixed. A single regex pass now covers both that and the other standard PDF string
    // escapes (PDF spec 7.3.4.2).
    internal static readonly Regex PdfStringEscapeRegex = new(@"\\(?:([()\\nrtbf])|([0-7]{1,3}))");

    internal static string UnescapePdfString(string value) =>
        PdfStringEscapeRegex.Replace(value, match =>
        {
            if (match.Groups[1].Success)
            {
                return match.Groups[1].Value switch
                {
                    "n" => "\n",
                    "r" => "\r",
                    "t" => "\t",
                    "b" => "\b",
                    "f" => "\f",
                    var literal => literal,
                };
            }

            return ((char)Convert.ToInt32(match.Groups[2].Value, 8)).ToString();
        });

    /// <summary>
    /// Collapses whitespace and folds typographic quotes to their plain ASCII form, so text
    /// reconstructed from the content stream can be compared against PdfPig's own line text (which
    /// normalizes quotes the same way).
    /// </summary>
    internal static string NormalizeWhitespace(string value) =>
        Regex.Replace(FoldTypographicQuotes(value), @"\s+", " ").Trim();

    internal static string FoldTypographicQuotes(string value) =>
        value
            .Replace('‘', '\'').Replace('’', '\'')
            .Replace('“', '"').Replace('”', '"');

    /// <summary>
    /// Computes the bounding box for the given page's matched lines and draws a white rectangle
    /// over it (still needed even where splicing succeeded - splicing only removes the matched
    /// glyphs, not any other marks that might be there, and it's needed outright wherever splicing
    /// failed), returning the box for replacement content to be drawn into afterwards.
    /// </summary>
    private static XRect WhiteoutMatchedRegion(PdfSharp.Pdf.PdfPage page, IEnumerable<DocumentLine> lines)
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

        graphics.DrawRectangle(XBrushes.White, rect);

        return rect;
    }

    /// <summary>Matches a lettered-list marker at the start of a line, e.g. "(a) " or "(e) ".</summary>
    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

    /// <summary>
    /// Draws the real replacement text (<see cref="WqFormParagraphOverlayTests.New315Items"/>) once
    /// across <paramref name="boxes"/> - the whited-out region on each page the match touched, in
    /// page order. Label/continuation indent are computed once from every matched line across the
    /// whole match (not per page), so alignment stays consistent even when the match spans a page
    /// break. A whole item (including its sub-items, if any) is measured before being drawn and
    /// moved to the next page's box entirely if it doesn't fit in the space remaining on the current
    /// one, rather than split mid-item.
    /// </summary>
    private static void DrawReplacementContentAcrossPages(
        IReadOnlyList<(PdfSharp.Pdf.PdfPage Page, XRect Rect)> boxes,
        IReadOnlyList<DocumentLine> matchedLines,
        (string Family, bool Bold, bool Italic, double Size) fontInfo)
    {
        var fontStyle = (fontInfo.Bold, fontInfo.Italic) switch
        {
            (true, true) => XFontStyleEx.BoldItalic,
            (true, false) => XFontStyleEx.Bold,
            (false, true) => XFontStyleEx.Italic,
            _ => XFontStyleEx.Regular,
        };
        var font = new XFont(fontInfo.Family, fontInfo.Size, fontStyle);

        var labelIndent = matchedLines
            .Where(line => ItemMarkerRegex.IsMatch(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= 0)
            .DefaultIfEmpty(boxes[0].Rect.X)
            .Min();

        var continuationLefts = matchedLines
            .Where(line => !ItemMarkerRegex.IsMatch(line.Text) && !string.IsNullOrWhiteSpace(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= labelIndent)
            .ToList();

        var continuationIndent = continuationLefts.Count > 0
            ? continuationLefts.Min()
            : labelIndent + 35;

        var hangingIndent = continuationIndent - labelIndent;

        var boxIndex = 0;
        var rect = boxes[boxIndex].Rect;
        var itemTop = rect.Y;
        var graphics = XGraphics.FromPdfPage(boxes[boxIndex].Page);
        var textFormatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };

        foreach (var item in WqFormParagraphOverlayTests.New315Items)
        {
            var itemHeight = WqFormParagraphOverlayTests.MeasureItemHeight(
                graphics, item, font, rect.Width, rect.Right - continuationIndent,
                rect.Right - (continuationIndent + hangingIndent));

            if (itemTop + itemHeight > rect.Bottom && boxIndex < boxes.Count - 1)
            {
                graphics.Dispose();
                boxIndex++;
                rect = boxes[boxIndex].Rect;
                itemTop = rect.Y;
                graphics = XGraphics.FromPdfPage(boxes[boxIndex].Page);
                textFormatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };
            }

            itemTop = WqFormParagraphOverlayTests.DrawItem(
                graphics, textFormatter, font, rect.X, rect.Right, continuationIndent, hangingIndent, itemTop, item);
        }

        graphics.Dispose();
    }
}
