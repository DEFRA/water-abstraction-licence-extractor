using System.Text;
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

/// <summary>
/// POC that locates the "SpecialTermsWholeBlock" paragraph in a real WQ form PDF and replaces it
/// with the real new clause 3.1.5 text (see <see cref="New315Items"/>) by drawing a white
/// rectangle over the original and overlaying the new wording. Handles a match spanning a page
/// break by grouping matched lines by page and drawing one whiteout+overlay per page. Source PDFs
/// live outside the repo (~/Downloads/WQ__*.pdf); skips gracefully if none are present.
/// </summary>
public class WqFormParagraphOverlayTests(ITestOutputHelper testOutputHelper)
{
    /// <summary>Resolves the system Arial font for PdfSharp, which has no built-in font lookup.</summary>
    /// <summary>
    /// Resolves the small set of font families actually seen in these real WQ form PDFs (Arial and
    /// Times New Roman, each with Bold/Italic variants) to their macOS system font files - the only
    /// families WqFormSpliceAndOverlayTests.GetFontInfoAtPosition currently detects from a
    /// document's own /BaseFont resource.
    /// </summary>
    internal sealed class LocalFontResolver : IFontResolver
    {
        private const string FontFolder = "/System/Library/Fonts/Supplemental/";

        private static readonly Dictionary<string, string> FacePaths = new(StringComparer.OrdinalIgnoreCase)
        {
            ["Arial"] = $"{FontFolder}Arial.ttf",
            ["Arial,Bold"] = $"{FontFolder}Arial Bold.ttf",
            ["Arial,Italic"] = $"{FontFolder}Arial Italic.ttf",
            ["Arial,BoldItalic"] = $"{FontFolder}Arial Bold Italic.ttf",
            ["Times New Roman"] = $"{FontFolder}Times New Roman.ttf",
            ["Times New Roman,Bold"] = $"{FontFolder}Times New Roman Bold.ttf",
            ["Times New Roman,Italic"] = $"{FontFolder}Times New Roman Italic.ttf",
            ["Times New Roman,BoldItalic"] = $"{FontFolder}Times New Roman Bold Italic.ttf",
        };

        public byte[] GetFont(string faceName) => File.ReadAllBytes(
            FacePaths.TryGetValue(faceName, out var path) ? path : FacePaths["Arial"]);

        public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            var suffix = (isBold, isItalic) switch
            {
                (true, true) => ",BoldItalic",
                (true, false) => ",Bold",
                (false, true) => ",Italic",
                _ => string.Empty,
            };

            var faceName = $"{familyName}{suffix}";

            return FacePaths.ContainsKey(faceName)
                ? new FontResolverInfo(faceName)
                : new FontResolverInfo("Arial");
        }
    }

    static WqFormParagraphOverlayTests()
    {
        GlobalFontSettings.FontResolver ??= new LocalFontResolver();
    }

    /// <summary>Pool of standard lorem-ipsum sentences of varying length used as placeholder text.</summary>
    internal static readonly string[] FillerSentences =
    [
        "Lorem ipsum dolor sit amet, consectetur adipiscing elit.",
        "Sed do eiusmod tempor incididunt ut labore et dolore magna aliqua.",
        "Ut enim ad minim veniam, quis nostrud exercitation ullamco laboris nisi ut aliquip ex ea commodo consequat.",
        "Duis aute irure dolor in reprehenderit in voluptate velit esse cillum dolore eu fugiat nulla pariatur.",
        "Excepteur sint occaecat cupidatat non proident, sunt in culpa qui officia deserunt mollit anim id est laborum.",
        "Curabitur pretium tincidunt lacus, ut interdum lacus dapibus.",
        "Sed pretium blandit orci, in dapibus turpis dictum sit amet.",
        "Interdum et malesuada fames ac ante ipsum primis in faucibus.",
        "Vestibulum ante ipsum primis in faucibus orci luctus et ultrices posuere cubilia curae.",
        "Praesent nonummy mi in odio, in hendrerit risus.",
        "Aenean commodo ligula eget dolor, aenean massa cursus.",
        "Cum sociis natoque penatibus et magnis dis parturient montes, nascetur ridiculus mus.",
        "Donec quam felis, ultricies nec, pellentesque eu, pretium quis, sem.",
        "Nulla consequat massa quis enim, donec pede justo, fringilla vel, aliquet nec, vulputate eget, arcu.",
        "In enim justo, rhoncus ut, imperdiet a, venenatis vitae, justo.",
    ];

    /// <summary>
    /// Hands out filler text sized to a target length, advancing through <see cref="FillerSentences"/>
    /// each call so consecutive calls return different text. One instance is shared across a whole
    /// document's replacement pass, including across a page break.
    /// </summary>
    internal sealed class FillerTextCursor
    {
        private int _index;

        /// <summary>
        /// Returns whole sentences totalling at most <paramref name="targetLength"/> characters
        /// (always at least one sentence, even if that exceeds the target).
        /// </summary>
        public string Next(int targetLength)
        {
            var builder = new StringBuilder();

            while (true)
            {
                var candidate = FillerSentences[_index % FillerSentences.Length];

                if (builder.Length > 0
                    && builder.Length + 1 + candidate.Length > targetLength)
                {
                    break;
                }

                if (builder.Length > 0)
                {
                    builder.Append(' ');
                }

                builder.Append(candidate);
                _index++;
            }

            return builder.ToString();
        }
    }

    /// <summary>
    /// One item (or sub-item) of the real DWF 3.1.5 replacement text: a lettered marker (empty for
    /// the unlettered intro paragraph) plus its body text, and optionally a nested list of
    /// roman-numeral sub-items (e.g. item (b)'s (i)/(ii)/(iii)).
    /// </summary>
    internal sealed record New315Item(
        string Label, string Body, IReadOnlyList<(string Label, string Body)>? SubItems = null);

    /// <summary>
    /// The real replacement text for clause 3.1.5, transcribed from
    /// <c>~/Downloads/DWF updates NPS guide.pdf</c> (an unlettered intro paragraph followed by
    /// lettered items). Item (c) is deliberately excluded: its final sentence depends on the
    /// permit/variation's real issue date (whether to include it at all, and which calendar year to
    /// cite), which nothing in this pipeline captures today.
    /// </summary>
    internal static readonly New315Item[] New315Items =
    [
        new(
            "",
            "The permitted Dry Weather Flow limit in schedule 3 table S3.1 is set at the operator's " +
            "planned annual 80% exceeded daily volume discharged."),
        new(
            "a",
            "For compliance purposes an exceedance shall be recorded for a calendar year only when " +
            "the limit in effect on 31 December of that calendar year is exceeded by 90% or more of " +
            "the 'good' recorded Total Daily Volumes in that calendar year."),
        new(
            "b",
            "Up to and including 31 December 2025:",
            [
                (
                    "i",
                    "If an exceedance of the Dry Weather Flow limit is recorded in a calendar year " +
                    "then the operator shall, as soon as is reasonably practicable, investigate the " +
                    "reasons for the exceedance."
                ),
                (
                    "ii",
                    "The operator shall report the reasons for the exceedance to the Environment " +
                    "Agency and the steps that it proposes to take to restore compliance."
                ),
                (
                    "iii",
                    "An exceedance of the Dry Weather Flow limit shall not be recorded as a failure " +
                    "of the Dry Weather Flow limit in that calendar year if the operator takes " +
                    "appropriate steps to restore compliance."
                ),
            ]),
    ];

    /// <summary>Total character length of the given lines' text joined with single spaces.</summary>
    internal static int CombinedLength(IEnumerable<DocumentLine> lines) =>
        string.Join(" ", lines.Select(line => line.Text)).Length;

    private const double LineHeightSafetyFactor = 1.1;

    /// <summary>
    /// Estimates the rendered height of <paramref name="text"/> if word-wrapped at
    /// <paramref name="width"/> in <paramref name="font"/>, since PdfSharp's <see cref="XTextFormatter"/>
    /// exposes no measurement API of its own. Simulates wrapping by measuring each candidate line as
    /// one string (so kerning between words is accounted for), with a small line-height margin to
    /// approximate <see cref="XTextFormatter"/>'s own internal leading.
    /// </summary>
    internal static double MeasureWrappedHeight(XGraphics graphics, string text, XFont font, double width)
    {
        var lineHeight = graphics.MeasureString("Ag", font).Height * LineHeightSafetyFactor;
        var lines = 1;
        var currentLine = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var candidate = currentLine.Length == 0 ? word : $"{currentLine} {word}";

            if (currentLine.Length > 0 && graphics.MeasureString(candidate, font).Width > width)
            {
                lines++;
                currentLine = word;
            }
            else
            {
                currentLine = candidate;
            }
        }

        return lines * lineHeight;
    }

    /// <summary>
    /// Total rendered height of one <see cref="New315Item"/> including its sub-items (if any) and
    /// the inter-item gap after each - used to decide, before drawing anything, whether the whole
    /// item fits in the space remaining on the current page.
    /// </summary>
    internal static double MeasureItemHeight(
        XGraphics graphics, New315Item item, XFont font, double introWidth, double bodyWidth, double subBodyWidth)
    {
        var isIntro = string.IsNullOrEmpty(item.Label);
        var height = MeasureWrappedHeight(graphics, item.Body, font, isIntro ? introWidth : bodyWidth) + InterItemGap;

        if (item.SubItems == null)
        {
            return height;
        }

        foreach (var (_, subBody) in item.SubItems)
        {
            height += MeasureWrappedHeight(graphics, subBody, font, subBodyWidth) + InterItemGap;
        }

        return height;
    }

    /// <summary>
    /// Draws one <see cref="New315Item"/> - the unlettered intro as a plain block spanning
    /// <paramref name="leftEdge"/> to <paramref name="rightEdge"/>, a lettered item as a
    /// hanging-indent paragraph (label at <paramref name="leftEdge"/>, body at
    /// <paramref name="bodyLeft"/>) - then, if it has sub-items, each as its own hanging-indent
    /// paragraph one further indent step in (<paramref name="hangingIndent"/), matching the same
    /// label-width convention as the top level. Returns the Y position immediately below what was
    /// drawn, for the caller to continue from.
    /// </summary>
    internal static double DrawItem(
        XGraphics graphics,
        XTextFormatter textFormatter,
        XFont font,
        double leftEdge,
        double rightEdge,
        double bodyLeft,
        double hangingIndent,
        double top,
        New315Item item)
    {
        var isIntro = string.IsNullOrEmpty(item.Label);
        var width = isIntro ? rightEdge - leftEdge : rightEdge - bodyLeft;
        var height = MeasureWrappedHeight(graphics, item.Body, font, width);

        if (isIntro)
        {
            textFormatter.DrawString(item.Body, font, XBrushes.Black, new XRect(leftEdge, top, width, height));
        }
        else
        {
            var labelRect = new XRect(leftEdge, top, bodyLeft - leftEdge, height);
            var bodyRect = new XRect(bodyLeft, top, width, height);

            graphics.DrawString($"({item.Label})", font, XBrushes.Black, labelRect, XStringFormats.TopLeft);
            textFormatter.DrawString(item.Body, font, XBrushes.Black, bodyRect);
        }

        top += height + InterItemGap;

        if (item.SubItems == null)
        {
            return top;
        }

        var subBodyLeft = bodyLeft + hangingIndent;

        foreach (var (subLabel, subBody) in item.SubItems)
        {
            var subWidth = rightEdge - subBodyLeft;
            var subHeight = MeasureWrappedHeight(graphics, subBody, font, subWidth);
            var subLabelRect = new XRect(bodyLeft, top, subBodyLeft - bodyLeft, subHeight);
            var subBodyRect = new XRect(subBodyLeft, top, subWidth, subHeight);

            graphics.DrawString($"({subLabel})", font, XBrushes.Black, subLabelRect, XStringFormats.TopLeft);
            textFormatter.DrawString(subBody, font, XBrushes.Black, subBodyRect);
            top += subHeight + InterItemGap;
        }

        return top;
    }

    /// <summary>Yields every WQ__*.pdf file under ~/Downloads, or none if the folder doesn't exist.</summary>
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
    public async Task WhenRealWqFormFile_ThenSpecialTermsBlockIsReplacedWithRealText(string sourcePath)
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

        ReplaceMatchedBlockWithRealText(sourcePath, outputPath, matchedLines);

        testOutputHelper.WriteLine($"Wrote original copy to {originalCopyPath}");
        testOutputHelper.WriteLine($"Wrote edited PDF to {outputPath}");
        Assert.True(File.Exists(outputPath));
    }

    /// <summary>
    /// Removes page furniture (headers/footers) that got swept into a match spanning a page break.
    /// Per page, sorts lines top-to-bottom and keeps the leading contiguous run, stopping at the
    /// first vertical gap larger than a normal line-to-line gap.
    /// </summary>
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

    /// <summary>
    /// Copies the source PDF's pages into a fresh document (bypassing its edit-permission
    /// restriction), then for each page containing matched lines draws a white rectangle over
    /// their bounding box and overlays replacement text.
    /// </summary>
    private static void ReplaceMatchedBlockWithRealText(
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
            .OrderBy(group => group.Key);

        foreach (var pageGroup in linesByPage)
        {
            var page = document.Pages[pageGroup.Key - 1];

            // Bounding box is built from word-level coordinates rather than DocumentLine's own
            // Top/Right/Bottom/Left, which don't reliably span a line's full rendered width.
            // Words carrying the "not known" coordinate sentinel (-1) are filtered out first.
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

            // DocumentLine/word coordinates are PDF space (origin bottom-left, Y-up); XGraphics
            // draws in top-left/Y-down space, so Y is flipped against page height.
            var pageHeight = page.Height.Point;
            var rect = new XRect(left, pageHeight - top, right - left, top - bottom);

            graphics.DrawRectangle(XBrushes.White, rect);
            DrawReplacementContent(graphics, rect, pageGroup);
        }

        document.Save(outputPath);
    }

    /// <summary>Matches a lettered-list marker at the start of a line, e.g. "(a) " or "(e) ".</summary>
    private static readonly Regex ItemMarkerRegex = new(@"^\(([a-z])\)\s+", RegexOptions.IgnoreCase);

    /// <summary>
    /// Draws the real replacement text (<see cref="New315Items"/>): an unlettered intro paragraph
    /// followed by lettered items, each its own hanging-indent paragraph (label at the block's left
    /// edge, wrapped body text starting at the original continuation indent) sized to its own
    /// measured wrapped height. The original matched <paramref name="lines"/> are used only for
    /// their layout (label/continuation indent), never their content - the replacement text has its
    /// own fixed wording independent of what it's replacing.
    /// </summary>
    private static void DrawReplacementContent(XGraphics graphics, XRect rect, IEnumerable<DocumentLine> lines)
    {
        var lineList = lines.ToList();
        var font = new XFont("Arial", 10);

        // Label indent is the smallest left position among the marker lines themselves, not
        // rect.X, which can be dragged left by unrelated page furniture swept into the match.
        var labelIndent = lineList
            .Where(line => ItemMarkerRegex.IsMatch(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= 0)
            .DefaultIfEmpty(rect.X)
            .Min();

        // Continuation indent is the smallest left position among non-marker lines indented at
        // least as much as the label - excluding anything further left (e.g. swept-in page
        // furniture) that isn't real continuation text.
        var continuationLefts = lineList
            .Where(line => !ItemMarkerRegex.IsMatch(line.Text) && !string.IsNullOrWhiteSpace(line.Text))
            .SelectMany(line => line.Columns.SelectMany(column => column.Words))
            .Select(word => word.Coordinates.Left)
            .Where(value => value >= labelIndent)
            .ToList();

        var continuationIndent = continuationLefts.Count > 0
            ? continuationLefts.Min()
            : labelIndent + 35;

        var hangingIndent = continuationIndent - labelIndent;
        var textFormatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };
        var itemTop = rect.Y;

        foreach (var item in New315Items)
        {
            itemTop = DrawItem(
                graphics, textFormatter, font, rect.X, rect.Right, continuationIndent, hangingIndent, itemTop, item);
        }
    }

    /// <summary>Vertical gap drawn between one item's body and the next item's label.</summary>
    internal const double InterItemGap = 6;

    /// <summary>
    /// Splits a page's matched lines into per-marker groups: a marker line ("(a) ...") starts a
    /// new group, and any following non-marker lines (a wrapped continuation) join that group.
    /// </summary>
    internal static List<List<DocumentLine>> GroupLinesByMarker(IReadOnlyList<DocumentLine> lineList)
    {
        var groups = new List<List<DocumentLine>>();

        foreach (var line in lineList)
        {
            if (groups.Count == 0 || ItemMarkerRegex.IsMatch(line.Text))
            {
                groups.Add([]);
            }

            groups[^1].Add(line);
        }

        return groups;
    }
}
