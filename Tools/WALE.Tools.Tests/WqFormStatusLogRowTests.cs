using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;
using WALE.ProcessFile.Services.Docnet;
using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.Services;
using Xunit.Abstractions;
using PdfDocumentOpenMode = PdfSharp.Pdf.IO.PdfDocumentOpenMode;

namespace WALE.Tools.Tests;

/// <summary>
/// POC: appends a new entry to a "Status log of the permit" table - "Permit modified" / the
/// permit number / today's date / a one-line comment - as real vector text and border rects, not
/// an image overlay. Runs across the whole WQ sample corpus
/// (<see cref="WqFormParagraphOverlayTests.SampleFiles"/>): row height and baseline offsets are
/// fixed constants (the same document generator produces this table with consistent typography),
/// but column X positions vary by table width per document, so those are derived per-file from
/// the table's own vertical divider rects rather than hardcoded. Unlike the 3.3.x clause insertion
/// in <see cref="WqFormPageSplitTests"/>, this never needs a page split: the table has ample blank
/// space below its last row before the page's closing note and footer. The only existing content
/// that has to move is the closing note and the blank placeholder paragraphs below it, shifted
/// down by exactly one row's height. The table itself can span a page break with "Status log of
/// the permit" repeated as a continuation header - insertion always targets the page the closing
/// note actually falls on, not wherever the heading first appears.
/// </summary>
public class WqFormStatusLogRowTests(ITestOutputHelper testOutputHelper)
{
    // Matches an existing "Permit modified" row's own shape: a 2-line Description cell (title +
    // permit number) with the Date/Comments cells sharing the title's own baseline. Assumed
    // constant across the corpus (same document generator/typography); only column X positions
    // vary per document.
    private const double RowHeight = 32.04;
    private const double TitleLineOffsetFromTop = 12.24;
    private const double PermitNumberLineOffsetFromTop = 26.76;

    // The gap between a column's own divider rect and where that column's text actually starts -
    // identical for all three columns, so column text X positions are derived as border X + this
    // padding rather than hardcoded too.
    private const double ColumnTextPadding = 4.68;
    private const double BorderThickness = 0.48;

    // A real table-row cell rect is this tall even for a single-line row; the table's own thin
    // (~0.48pt) divider/rule rects fall well under it - used to tell the two apart when scanning
    // for border rects, independent of this document's own actual row heights.
    private const double MinimumCellRectHeight = 5;

    private static readonly Regex RectRegex = new(@"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+re");

    // "q"/"Q" as standalone operators (whitespace on both sides) - excludes the letter q/Q
    // appearing inside literal text runs, e.g. "(quality)" or "(Q1)".
    private static readonly Regex QOperatorRegex = new(@"(?<=\s)(q|Q)(?=\s)");

    // Matches the WHOLE heading line only ("Status log of [the ]permit"), not a multi-permit
    // consolidation's own per-permit heading ("Status log of permit A: 002728").
    private static readonly Regex StatusLogHeadingRegex = new(
        @"^Status log of( the)? permit$", RegexOptions.IgnoreCase);

    static WqFormStatusLogRowTests()
    {
        GlobalFontSettings.FontResolver ??= new WqFormParagraphOverlayTests.LocalFontResolver();
    }

    [Fact]
    public async Task WhenAw1nf618_ThenStatusLogRowIsAppended()
    {
        var sourcePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads",
            "wq__aw1nf618__c5788dbb-cd41-0870-32d6-6d3eae859f00.pdf");

        var comparisonFolder = Path.Combine(AppContext.BaseDirectory, "ComparisonOutput");
        Directory.CreateDirectory(comparisonFolder);
        var outputPath = Path.Combine(comparisonFolder, "wq__aw1nf618-status-log-row.pdf");

        var (success, message) = await TryAddStatusLogRowAsync(
            sourcePath,
            outputPath,
            permitNumber: "AW1NF/618",
            description: "Permit modified",
            date: DateTime.Today,
            comment: "Lorem ipsum dolor sit amet.");

        testOutputHelper.WriteLine(success ? $"OK: {message}" : $"FAILED: {message}");
        Assert.True(success, message);
    }

    /// <summary>
    /// Chains the status log row onto the existing 3.1.5-replacement + 3.3.x-insertion combined
    /// output (<see cref="WqFormPageSplitTests.WhenRealWqFormFile_ThenReplacementAndInsertionAreCombined"/>)
    /// so a reviewer can see all three real-text features applied to one document at once - the
    /// status log table (page 3) and the two other features (later pages) never touch the same
    /// content, so this is a straight append onto whatever that combined file already contains.
    /// </summary>
    [Fact]
    public async Task WhenAw1nf618Combined_ThenStatusLogRowIsAlsoAppended()
    {
        var downloads = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads");
        var sourcePath = Path.Combine(
            downloads, "wq__aw1nf618__c5788dbb-cd41-0870-32d6-6d3eae859f00-combined.pdf");

        Assert.True(File.Exists(sourcePath), $"expected the combined output to already exist at {sourcePath}.");

        var comparisonFolder = Path.Combine(AppContext.BaseDirectory, "ComparisonOutput");
        Directory.CreateDirectory(comparisonFolder);
        var outputPath = Path.Combine(comparisonFolder, "wq__aw1nf618-combined-with-status-log.pdf");

        var (success, message) = await TryAddStatusLogRowAsync(
            sourcePath,
            outputPath,
            permitNumber: "AW1NF/618",
            description: "Permit modified",
            date: DateTime.Today,
            comment: "Lorem ipsum dolor sit amet.");

        testOutputHelper.WriteLine(success ? $"OK: {message}" : $"FAILED: {message}");
        Assert.True(success, message);
    }

    /// <summary>
    /// Runs the status log row append against every real sample file
    /// (<see cref="WqFormParagraphOverlayTests.SampleFiles"/>). No formal permit-number extraction
    /// exists yet, so the permit number is a best-effort stand-in taken from the filename's own
    /// "wq__&lt;code&gt;__&lt;uuid&gt;" segment (uppercased, no inserted slash) - good enough to
    /// sanity-check the row-insertion mechanism across the corpus, not a correctly formatted
    /// production value.
    /// </summary>
    [Theory]
    [MemberData(nameof(WqFormParagraphOverlayTests.SampleFiles), MemberType = typeof(WqFormParagraphOverlayTests))]
    public async Task WhenRealWqFormFile_ThenStatusLogRowIsAppended(string sourcePath)
    {
        var filename = Path.GetFileName(sourcePath);
        var comparisonFolder = Path.Combine(AppContext.BaseDirectory, "ComparisonOutput");
        Directory.CreateDirectory(comparisonFolder);
        var outputPath = Path.Combine(
            comparisonFolder, $"{Path.GetFileNameWithoutExtension(filename)}-status-log-row.pdf");

        var permitNumber = DerivePermitNumberFromFilename(filename);

        var (success, message) = await TryAddStatusLogRowAsync(
            sourcePath,
            outputPath,
            permitNumber,
            description: "Permit modified",
            date: DateTime.Today,
            comment: "Lorem ipsum dolor sit amet.");

        var line = success ? $"OK {filename}: {message}" : $"SKIPPED {filename}: {message}";
        testOutputHelper.WriteLine(line);

        lock (CorpusResultsLock)
        {
            File.AppendAllText(CorpusResultsPath, line + Environment.NewLine);
        }
    }

    private static readonly object CorpusResultsLock = new();
    private static readonly string CorpusResultsPath =
        "/private/tmp/claude-501/-Users-edwardbutler-Documents-GitHub-wale/4f38b433-5ad9-40d2-b2ea-a18538294ced/scratchpad/corpus-status-log-results.txt";

    private static string DerivePermitNumberFromFilename(string filename)
    {
        var parts = Path.GetFileNameWithoutExtension(filename).Split("__");

        return parts.Length >= 2 ? parts[1].ToUpperInvariant() : filename;
    }

    private async Task<(bool Success, string Message)> TryAddStatusLogRowAsync(
        string sourcePath,
        string outputPath,
        string permitNumber,
        string description,
        DateTime date,
        string comment)
    {
        var folder = Path.GetDirectoryName(sourcePath)! + "/";
        var fileService = new LocalFileService(folder);
        var cacheService = new WALE.ProcessFile.Services.Cache.FileSystemCacheService("Cache/");
        var outputService = new WALE.ProcessFile.Services.Output.FileSystemOutputService("Output/");
        var documentService = new PdfPigNoOcrPdfDocumentService();
        var docnetAlternativeDocumentService = new DocnetNoOcrAlternativePdfDocumentService();
        var noOcrDataExtractorService = new PdfPigNoOcrDataExtractorService();

        var lookupConfiguration = new WALE.ProcessFile.Core.Configuration.LookupConfiguration(
            [], [], fileService, cacheService, outputService, null!, null!,
            WALE.ProcessFile.Core.Constants.GeneralConstants.UnsetRegionCode,
            DateTime.Now, skipFileIfMoreThenPages: 100, useLockExclusivity: false);

        var pdfDocument = await noOcrDataExtractorService.GetPdfDocumentAsync(
            Path.GetFileName(sourcePath), Guid.NewGuid(), outputService, cacheService,
            documentService, docnetAlternativeDocumentService, lookupConfiguration, -1);

        if (pdfDocument == null)
        {
            return (false, "could not open pdf document.");
        }

        var lines = await noOcrDataExtractorService
            .GetTextLinesFromPdfAndSaveScreenshotsPageTextLinesAndMetadataAsync(
                pdfDocument, cacheService, outputService, -1);

        // Most of the corpus titles this "Status log of the permit"; a few drop "the" ("Status log
        // of permit"). Both are accepted, but only as the WHOLE heading line - documents that
        // consolidate multiple permits title each one's own table "Status log of permit A: <n>" /
        // "...B: <n>", which also starts with this text but is deliberately NOT matched here: with
        // several tables and one shared closing note, there is no way to tell which permit's table
        // the new row's own permit number (taken from the filename) actually belongs to.
        var hasStatusLogTable = lines.Any(line =>
            StatusLogHeadingRegex.IsMatch(line.Text.Trim()));

        if (!hasStatusLogTable)
        {
            return (false, "could not find the \"Status log of the permit\" table.");
        }

        // The table can span a page break, with "Status log of the permit" repeated as a
        // continuation header on the next page - anchoring to the FIRST page it appears on would
        // miss the note entirely whenever the table (and so the note that closes it) actually ends
        // on a later page. The note's own page is what matters for insertion: that is where the
        // table's closing border, its last row, and the note itself all actually live.
        var closingNoteLine = lines.FirstOrDefault(line =>
            line.Text.Contains("End of introductory note", StringComparison.OrdinalIgnoreCase));

        if (closingNoteLine == null)
        {
            return (false, "could not find \"End of introductory note\" to anchor the insertion point.");
        }

        var statusLogPageNumber = closingNoteLine.PageNumber;

        // Import, not Modify - the source PDF's own owner-password/permissions flags reject
        // in-place modification. Copying pages into a brand new document (the same workaround
        // WqFormPageSplitTests/WqFormSpliceAndOverlayTests already use) sidesteps that.
        using var sourceDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        using var document = new PdfSharp.Pdf.PdfDocument();

        foreach (var sourcePage in sourceDocument.Pages)
        {
            document.AddPage(sourcePage);
        }

        var targetPage = document.Pages[statusLogPageNumber - 1];
        var pageHeight = targetPage.Height.Point;

        var contentDict = targetPage.Contents.Elements.GetDictionary(0);
        var content = Encoding.Latin1.GetString(contentDict.Stream!.UnfilteredValue);
        var toUnicodeMap = WqFormSpliceAndOverlayTests.BuildToUnicodeMap(targetPage);

        var (_, noteFound, noteTjIndex) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            content, closingNoteLine.Text, toUnicodeMap);

        if (!noteFound)
        {
            return (false, "found \"End of introductory note\" via PdfPig but couldn't locate its operator in the raw content stream.");
        }

        // TryRemoveLineOperator's index can land on a preceding blank filler operator ("[( )] TJ",
        // which every cell in this template also emits) rather than the note's own text, since a
        // blank accumulation normalizes to empty and is skipped rather than validated. Scan forward
        // for the first operator that actually reconstructs to non-blank text.
        var tjMatches = WqFormSpliceAndOverlayTests.TjOperatorRegex.Matches(content).Cast<Match>().ToList();
        var realContentMatch = tjMatches
            .Where(m => m.Index >= noteTjIndex)
            .FirstOrDefault(m => WqFormSpliceAndOverlayTests.NormalizeWhitespace(
                WqFormSpliceAndOverlayTests.ReconstructLiteralText(m.Value, toUnicodeMap)).Length > 0);
        var realContentIndex = realContentMatch?.Index ?? noteTjIndex;

        // Splitting mid-fragment (at realContentIndex's own TJ) separates the note's paint call
        // from the Tm that positions it, since only Tm operators get shifted - the text would then
        // render at its OLD, un-shifted position under the new row. Backing up to the fragment's
        // own BT keeps its Tf/Tm/TJ together as one atomic unit. A fragment can also be wrapped in
        // its own clip prefix immediately before that BT (marked-content style
        // "EMC q <re> W* n BDC q <re> W* n", or a bare "q <re> W* n" with no marked content at
        // all) - splitting AT the BT then leaves that clip-opening prefix in headContent, so
        // inserted content inherits the fragment's own (unrelated) clip rect and renders invisibly
        // (pdftotext, which ignores clipping, still extracts it fine). FindOutermostOpenQIndex
        // below scans the actual q/Q graphics-state stack back from the BT and, if any "q" is
        // still unmatched by a "Q" there, backs the split up to the outermost such "q" - keeping
        // headContent's own q/Q balance clean regardless of how the clip was opened.
        var btIndex = content[..realContentIndex].LastIndexOf("BT", StringComparison.Ordinal);

        if (btIndex < 0)
        {
            btIndex = realContentIndex;
        }

        var noteIndex = FindOutermostOpenQIndex(content, btIndex) ?? btIndex;

        // The new row's own clip-top is exactly where the table's border currently closes off
        // (the existing thin divider rects at the last row's own bottom edge, which sit
        // immediately before the closing note in the raw content stream) - reused as-is as the
        // new row's top border too, the same way every other internal divider in this table
        // already does double duty between adjacent rows.
        var newRowClipTop = FindTableBottomBorderY(content[..noteIndex]);

        if (newRowClipTop == null)
        {
            return (false, "could not find the table's own closing border.");
        }

        var newRowClipBottom = newRowClipTop.Value - RowHeight;

        var columnBorders = FindColumnBorders(content[..noteIndex], newRowClipTop.Value, newRowClipBottom);

        if (columnBorders == null)
        {
            return (false, "could not determine the table's own column divider positions.");
        }

        var descriptionColumnX = columnBorders.Value.Left + ColumnTextPadding;
        var dateColumnX = columnBorders.Value.DescriptionDate + ColumnTextPadding;
        var commentsColumnX = columnBorders.Value.DateComments + ColumnTextPadding;

        // The row's own font resource name (e.g. "/F1") - reused directly rather than resolved to
        // an XFont, so the new text is written as plain BT/Tf/Tm/Tj operators in this document's
        // own native coordinate space instead of going through XGraphics' own (font-metric-
        // dependent, and here mismatched) baseline-to-top conversion for DrawString. Writing
        // literal ASCII bytes against a Type0/Identity-H (CID) font would be misread as 2-byte
        // glyph-ID pairs, not characters - some documents embed both a WinAnsi and a CID instance
        // of "the same" font under different resource names, and the one active immediately before
        // the split point isn't always the WinAnsi one, so the nearest simple-font Tf is preferred.
        var fontDictionary = targetPage.Resources.Elements.GetDictionary("/Font");
        var tfMatches = WqFormSpliceAndOverlayTests.TfOperatorRegex.Matches(content[..noteIndex]).Cast<Match>().Reverse();
        var lastTf = tfMatches.FirstOrDefault(m => !IsType0Font(fontDictionary, m.Groups[1].Value));

        if (lastTf == null)
        {
            return (false, "no simple (non-CID) font found active before the closing note.");
        }

        var fontResourceName = lastTf.Groups[1].Value;

        // Some documents in this corpus (Word/LibreOffice-style generators) emit a nominal Tf size
        // of 1 and bake the real, effective size into the text matrix's own scale instead - taking
        // the raw Tf operand as-is rendered near-invisible, glyph-collapsed text for those files.
        // Our own row's Tm is always an unscaled "1 0 0 1 x y", so the Tf value it needs is this
        // effective size, not the nominal one.
        var nominalFontSize = double.TryParse(lastTf.Groups[2].Value, out var parsedNominalSize) ? parsedNominalSize : 10;
        var lastTm = WqFormSpliceAndOverlayTests.TextMatrixRegex.Matches(content[..noteIndex]).Cast<Match>().LastOrDefault();
        var textMatrixScale = lastTm != null && double.TryParse(lastTm.Groups[1].Value, out var parsedScale)
            ? Math.Abs(parsedScale)
            : 1;
        var fontSize = (nominalFontSize * textMatrixScale).ToString("0.####");

        // Tf is a persistent graphics-state parameter, not reset by BT/ET - it carries over into
        // whatever text object comes next. Some of this corpus's original content (e.g. the
        // closing note itself) relies on inheriting a still-nominal Tf and applies its own Tm
        // scale on top, rather than setting its own Tf. Leaving our own row's *effective* Tf value
        // active would then compound with that later block's own Tm scale, rendering it at many
        // times its intended size - so the original nominal Tf is explicitly restored afterwards.
        var newRowContent =
            TextOperator(fontResourceName, fontSize, descriptionColumnX, newRowClipTop.Value - TitleLineOffsetFromTop, description) +
            TextOperator(fontResourceName, fontSize, descriptionColumnX, newRowClipTop.Value - PermitNumberLineOffsetFromTop, permitNumber) +
            TextOperator(fontResourceName, fontSize, dateColumnX, newRowClipTop.Value - TitleLineOffsetFromTop, date.ToString("dd/MM/yyyy")) +
            TextOperator(fontResourceName, fontSize, commentsColumnX, newRowClipTop.Value - TitleLineOffsetFromTop, comment) +
            $"BT\n/{fontResourceName} {lastTf.Groups[2].Value} Tf\nET\n";

        var headContent = content[..noteIndex];
        var tailContent = content[noteIndex..];
        var shiftedTailContent = ShiftTextMatrixOperatorsDown(tailContent, RowHeight);

        // headContent's split point can land right after a bare "EMC" token with no guaranteed
        // trailing separator - gluing newRowContent's own leading "BT" straight onto it would
        // produce an invalid "EMCBT" operator, so a newline is inserted explicitly. UnfilteredValue
        // is read-only - writing raw bytes back via Value while /Filter still claims FlateDecode
        // leaves the two inconsistent, so /Filter is dropped too.
        contentDict.Stream.Value = Encoding.Latin1.GetBytes(headContent + "\n" + newRowContent + shiftedTailContent);
        contentDict.Elements.Remove("/Filter");

        using (var graphics = XGraphics.FromPdfPage(targetPage))
        {
            DrawTableRowBorders(graphics, columnBorders.Value, newRowClipTop.Value, newRowClipBottom, pageHeight);
        }

        document.Save(outputPath);

        return (true,
            $"appended \"{description}\" / {permitNumber} / {date:dd/MM/yyyy} row to page {statusLogPageNumber}, " +
            $"shifted the closing note and blank paragraphs down by {RowHeight}pt.");
    }

    /// <summary>
    /// A Type0 font's content is a sequence of multi-byte glyph-ID codes, not character bytes - the
    /// literal ASCII text <see cref="TextOperator"/> writes would be misread as 2-byte CID pairs
    /// against one, producing garbled or invisible glyphs instead of the intended characters.
    /// </summary>
    private static bool IsType0Font(PdfDictionary? fontDictionary, string resourceName) =>
        fontDictionary?.Elements.GetDictionary($"/{resourceName}")?.Elements.GetName("/Subtype") == "/Type0";

    /// <summary>
    /// The table's own closing border sits as a group of thin (~0.48pt) rects immediately
    /// before the closing note in the content stream, including the last row's own full-height
    /// vertical divider rects (drawn from that row's own bottom edge upward) - the nearest one
    /// to <paramref name="contentBeforeNote"/>'s own end gives the table's current bottom edge
    /// directly as that rect's own y (its bottom, not y + height, which is the OLD last row's
    /// own top instead).
    /// </summary>
    private static double? FindTableBottomBorderY(string contentBeforeNote)
    {
        var fullHeightRect = RectRegex.Matches(contentBeforeNote)
            .Cast<Match>()
            .LastOrDefault(m => double.Parse(m.Groups[4].Value) > MinimumCellRectHeight);

        return fullHeightRect == null ? null : double.Parse(fullHeightRect.Groups[2].Value);
    }

    /// <summary>
    /// Replays the q/Q graphics-state stack from the start of the content up to
    /// <paramref name="upToIndex"/> and returns the position of the outermost "q" still unmatched
    /// by a "Q" at that point (or null if the stack is empty, i.e. no clip is active) - splitting
    /// before this index guarantees headContent's own q/Q balance is clean, regardless of what
    /// mechanism (marked content, a bare clip, nested clips) opened it.
    /// </summary>
    private static int? FindOutermostOpenQIndex(string content, int upToIndex)
    {
        var openQIndices = new List<int>();

        foreach (Match match in QOperatorRegex.Matches(content[..upToIndex]))
        {
            if (match.Value == "q")
            {
                openQIndices.Add(match.Index);
            }
            else if (openQIndices.Count > 0)
            {
                openQIndices.RemoveAt(openQIndices.Count - 1);
            }
        }

        return openQIndices.Count > 0 ? openQIndices[0] : null;
    }

    /// <summary>
    /// Most of this corpus draws the table's column dividers as thin vertical rects (width under a
    /// point) spanning the row band - collecting the distinct X positions of the ones overlapping
    /// [<paramref name="rowBottom"/>, <paramref name="rowTop"/>] gives the left edge, the two
    /// internal dividers, and the right edge directly. Some documents draw no separate divider
    /// rects at all - each column's own cell clip rect sits edge-to-edge against its neighbour
    /// instead, so the boundary is implicit in where one clip rect ends and the next begins; when
    /// no thin dividers are found, the three (Description/Date/Comments) clip rects' own X
    /// positions and the last one's right edge serve the same purpose.
    /// </summary>
    private static (double Left, double DescriptionDate, double DateComments, double Right)? FindColumnBorders(
        string contentBeforeNote, double rowTop, double rowBottom)
    {
        var rectsInRowBand = RectRegex.Matches(contentBeforeNote)
            .Cast<Match>()
            .Select(m => new
            {
                X = double.Parse(m.Groups[1].Value),
                Y = double.Parse(m.Groups[2].Value),
                Width = double.Parse(m.Groups[3].Value),
                Height = double.Parse(m.Groups[4].Value)
            })
            .Where(r => r.Y <= rowTop + 1 && r.Y + r.Height >= rowBottom - 1)
            .ToList();

        var dividerXPositions = rectsInRowBand
            .Where(r => r.Width < 2)
            .Select(r => r.X)
            .Distinct()
            .OrderBy(x => x)
            .ToList();

        if (dividerXPositions.Count == 4)
        {
            return (dividerXPositions[0], dividerXPositions[1], dividerXPositions[2], dividerXPositions[3]);
        }

        var cellClipRects = rectsInRowBand
            .Where(r => r.Width >= 2 && r.Height > MinimumCellRectHeight)
            .Select(r => (r.X, r.Width))
            .Distinct()
            .OrderBy(r => r.X)
            .ToList();

        if (cellClipRects.Count == 3)
        {
            return (cellClipRects[0].X, cellClipRects[1].X, cellClipRects[2].X, cellClipRects[2].X + cellClipRects[2].Width);
        }

        return null;
    }

    /// <summary>
    /// Shifts every text matrix's own Y operand down by <paramref name="amount"/> - used to move
    /// already-correct content (the closing note, the blank placeholder paragraphs below it)
    /// wholesale rather than re-drawing it, so its own text/kerning is untouched.
    /// </summary>
    private static string ShiftTextMatrixOperatorsDown(string content, double amount)
    {
        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

        return tmRegex.Replace(content, match =>
        {
            var y = double.Parse(match.Groups[6].Value) - amount;

            return $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value} " +
                   $"{match.Groups[4].Value} {match.Groups[5].Value} {y} Tm";
        });
    }

    /// <summary>
    /// One BT/Tf/Tm/Tj/ET text-show block, written directly in this document's own native
    /// (bottom-up) coordinate space - avoids XGraphics' DrawString, whose Y is the top of the
    /// text's bounding box rather than its baseline and needs a font-metric-dependent
    /// conversion this document's own font didn't resolve cleanly. Plain literal text, no
    /// kerning - a purely cosmetic difference from the surrounding rows' own kerned text.
    /// </summary>
    private static string TextOperator(string fontResourceName, string fontSize, double x, double y, string text)
    {
        var escaped = text.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");

        return $"BT\n/{fontResourceName} {fontSize} Tf\n1 0 0 1 {x} {y} Tm\n0 g\n[({escaped})] TJ\nET\n";
    }

    private static void DrawTableRowBorders(
        XGraphics graphics,
        (double Left, double DescriptionDate, double DateComments, double Right) columnBorders,
        double clipTop,
        double clipBottom,
        double pageHeight)
    {
        var rowHeight = clipTop - clipBottom;

        foreach (var x in new[] { columnBorders.Left, columnBorders.DescriptionDate, columnBorders.DateComments, columnBorders.Right })
        {
            DrawNativeRect(graphics, x, clipBottom, BorderThickness, rowHeight, pageHeight);
        }

        DrawNativeRect(
            graphics, columnBorders.Left, clipBottom - BorderThickness,
            columnBorders.Right - columnBorders.Left, BorderThickness, pageHeight);
    }

    private static void DrawNativeRect(XGraphics graphics, double x, double yBottom, double width, double height, double pageHeight)
    {
        graphics.DrawRectangle(XBrushes.Black, x, pageHeight - yBottom - height, width, height);
    }
}
