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
// its string content via regex and comparing against the line text PdfPig already gave us - no
// font-width/text-matrix interpretation. Handles both literal WinAnsi/TrueType text and
// Identity-H/Type0 glyph IDs (resolved through the font's own embedded /ToUnicode CMap - see
// BuildToUnicodeMap/ParseToUnicodeCMap) - so it covers both real sample files' fonts. What it
// still can't do: a Type0 font with no /ToUnicode entry at all (nothing to resolve a glyph ID
// against - genuinely undecodable without a full glyph-outline interpreter), or a line whose text
// spans across more than one Tj/TJ operator (only a whole-operator match is attempted).
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
    // single-string Tj (literal "(...)" or hex "<...>" - the latter needed for Identity-H/Type0
    // fonts, whose glyph IDs are always hex strings). Doesn't handle a literal, unescaped ']'
    // inside a string (would end the array match early) - a real limitation of a regex-based
    // approach, acceptable for this POC.
    private static readonly Regex TjOperatorRegex = new(
        @"\[(?:[^\[\]]|\\.)*\]\s*TJ|\((?:[^()\\]|\\.)*\)\s*Tj|<[0-9A-Fa-f\s]*>\s*Tj",
        RegexOptions.Singleline);

    // A literal "(...)" run or a hex "<...>" run, in the order they appear within one operator -
    // order matters because a TJ array interleaves them with kerning numbers between glyphs/words.
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

    // How many consecutive operators to accumulate while looking for one line's text - a generous
    // cap, not a precisely-tuned one; real lines seen so far need at most 4 (marker + space + "If"
    // + body, on WQ__003101).
    private const int MaxOperatorsPerLine = 10;

    // A PdfPig "line" doesn't always correspond to one Tj/TJ operator - confirmed empirically on
    // WQ__003101, whose generator draws a lettered-list marker, the following space, and the
    // paragraph body as three-or-more SEPARATE operators (each its own BT/Tm/TJ/ET block) rather
    // than one. The original single-operator match missed this entirely: it would find the long
    // body operator alone (already >90% of the target's length on its own) and delete only that,
    // silently leaving the "(a)"/"If" marker operators behind - a false "SPLICED" that still left
    // real text in the file, confirmed via pdftotext showing leftover fragments under the new
    // overlay. Fixed by greedily accumulating forward from every possible starting operator until
    // the combined reconstructed text covers the whole target line, then deleting the entire
    // matched run in one go. Trying starting points in document order and returning on the first
    // success means a line split across several operators is matched from its earliest operator
    // (the marker), not from partway through - so the whole run gets removed, not just the tail.
    //
    // Growing accumulation must stay an exact prefix of the target throughout, not just "contain
    // it somewhere" - confirmed necessary empirically: WQ__003101's "3.1.4" section-heading
    // operator sits immediately before the "(a)" marker operator, and accumulating from there
    // ("3.1.4 (a) If the measured...then the") still legitimately CONTAINS the real target text,
    // so a loose either-direction Contains() check (an earlier version of this method) accepted
    // it as a match starting at "3.1.4" and deleted the heading along with the real line. Requiring
    // the accumulation to only ever grow along the target's own leading edge - and, once it's
    // reached the target's length, to literally start with the target - rules that out while still
    // allowing a final operator to bundle a little extra trailing content beyond the line's end
    // (the 1.5x cap below).
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
                    // This start doesn't lead toward the target - no point growing it further.
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

    // Concatenates the literal "(...)" and hex "<...>" string runs within one Tj/TJ operator, in
    // the order they appear, ignoring the kerning numbers between them - e.g.
    // "[(If )6.4(the)8( )-6.4(mea)8(s)...]TJ" reconstructs to "If the mea s...". A literal run is
    // WinAnsi text (Windows-1252 - see ToWindows1252); a hex run is a string of fixed 2-byte
    // Identity-H glyph IDs, resolved to real characters through the font's own /ToUnicode CMap
    // (see BuildToUnicodeMap/ParseToUnicodeCMap) - already correct Unicode, no further decoding
    // needed. Spaces inside a literal run (like the one after "If") are preserved as-is since PDF
    // encodes them as literal space characters within a run.
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

    // Reads every Type0 font's embedded /ToUnicode CMap on this page and merges them into one
    // CID -> character lookup. Assumes CIDs don't collide meaningfully across fonts sharing a
    // page - true for both real sample files here, which each have at most one embedded
    // Identity-H font; a POC-level simplification, not a general solution for documents mixing
    // several distinct embedded CID fonts.
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

    // Parses the two mapping constructs a /ToUnicode CMap uses (PDF spec 9.10.3): "bfchar" is a
    // flat list of one-CID-to-one-string mappings; "bfrange" maps a contiguous span of CIDs either
    // to a single base codepoint (incrementing per CID) or to an explicit array of per-CID
    // strings. Scoped to just the beginbfchar/beginbfrange blocks - the codespacerange line (e.g.
    // "<0000> <FFFF>") would otherwise coincidentally match the same "<hex> <hex>" shape as a
    // bfchar entry.
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

    // PDF ToUnicode destination strings are UTF-16BE - BigEndianUnicode decodes that directly,
    // with no manual byte-swapping needed.
    private static string HexToUtf16String(string hex) =>
        Encoding.BigEndianUnicode.GetString(Convert.FromHexString(hex));

    private static string OffsetHexCodepoint(string dstHex, int offset)
    {
        var value = Convert.ToInt64(dstHex, 16) + offset;
        return HexToUtf16String(value.ToString($"X{dstHex.Length}"));
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
