using System.Text;
using System.Text.RegularExpressions;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;
using PdfSharp.Pdf.IO;
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
using static WALE.Tools.Helpers.PdfContentStreamHelper;
using PdfDocumentOpenMode = PdfSharp.Pdf.IO.PdfDocumentOpenMode;

namespace WALE.Tools.Tests;

/// <summary>
/// POC that inserts a new clause 3.3.10 immediately after the highest existing 3.3.x condition,
/// pushing whatever follows (the next heading-level line, e.g. "4 Information") onto a brand new
/// page as real vector content - not a raster image, not re-flowed/re-laid-out - then renumbers
/// every subsequent page's printed footer page number by +1, using the same in-place, same-length
/// text substitution technique ("Ryan's trick") already proven in
/// <see cref="WqFormSpliceAndOverlayTests"/>.
///
/// This targets the harder of the two clause-insertion problems investigated this session:
/// replacing existing 3.1.5 text (see <see cref="WqFormSpliceAndOverlayTests"/>) fits into
/// space the original document already allocated. A brand new 3.3.x condition allocates none -
/// it has to make room for itself. PDF-to-Word and PDF-to-HTML round trips were tried and
/// abandoned this session because reflowing corrupted table content; this proves real
/// content-stream relocation (moving already-correct vector operators wholesale, never
/// re-interpreting their layout) avoids that failure mode entirely.
///
/// Deliberately narrow scope, consistent with everything found this session about how varied
/// these templates are - see the per-file skip reasons logged by each test run:
/// - Needs a "3.3.\d+" clause to exist at all.
/// - The cut point (the first line back at the base margin after the highest 3.3.x clause) needs
///   a preceding absolute Tm somewhere in the page's content stream to anchor the relocation to -
///   true whether the cut point sits right at that Tm, or is reached from it via a chain of
///   relative TD/Td/T* moves (see <see cref="TryComputeAbsolutePositionAtCutPoint"/>, which
///   recovers the true position in the latter case).
/// - Digit-count-boundary renumbering (e.g. 9 -> 10) and same-digit-count renumbering are both
///   handled (excise+redraw vs. in-place substitution respectively).
/// </summary>
public class WqFormPageSplitTests(ITestOutputHelper testOutputHelper)
{
    private static readonly Regex ClauseNumberRegex = new(@"^3\.3\.(\d+)\b");
    private static readonly Regex PermitNumberLabelRegex = new(@"^Permit\s+number\b", RegexOptions.IgnoreCase);
    private static readonly Regex PermitNumberValueRegex = new(@"^[A-Za-z0-9/.\-]{5,20}$");

    /// <summary>
    /// Catches a reconstructed line that's actually the page's own running footer (a "Permit
    /// number" / value pair, or a "Consolidated Permit Number" / "Page N of M" pair) merged into
    /// one line by PdfPig's own Y-proximity line grouping - "Permit" and "number" together is
    /// never genuine clause-heading text, so this is safe to use as a general exclusion rather
    /// than needing this document's own permit number value to hand.
    /// </summary>
    private static bool LooksLikePermitFooterLine(string text) =>
        text.Contains("Permit", StringComparison.OrdinalIgnoreCase) &&
        text.Contains("number", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The real DWF data-quality condition, transcribed from
    /// <c>~/Downloads/DWF updates NPS guide.pdf</c>. Unlike the 3.1.5 replacement text (see
    /// <see cref="WqFormParagraphOverlayTests.New315Items"/>), this clause has no unlettered
    /// intro sentence - it goes straight from the clause number into (a).
    /// </summary>
    private static readonly WqFormParagraphOverlayTests.New315Item[] NewConditionItems =
    [
        new(
            "a",
            "Total Daily volumes shall be calculated from the average of the available 'good' 15 " +
            "minute flow readings taken from midnight to midnight where; Total Daily Volume (m3) " +
            "= {Sum of 'good' readings (l/s) / number of 'good' readings} x {86,400 (s) / 1000}. " +
            "Where there are 87 or more 'good' 15 minute flow readings the Total Daily Volume " +
            "shall be reported as 'good', where there are 1-86 'good' readings it shall be " +
            "reported as 'suspect' and where there are no 'good' readings as 'missing'."),
        new(
            "b",
            "The operator shall record all failures of the flow measurement system and any other " +
            "breaks in the flow record and the reasons for all issues, failures and breaks that " +
            "lead to missing or suspect Total Daily Volume records and all steps taken to " +
            "prevent a re-occurrence."),
        new(
            "c",
            "There shall be no more than 37 days and/or no more than 14 consecutive days with " +
            "'suspect' or 'missing' Total Daily Volumes in a calendar year, unless otherwise " +
            "agreed in writing by the Environment Agency."),
        new(
            "d",
            "All 15 minute flow readings shall be flagged as 'good', 'suspect' or 'missing' " +
            "using an appropriate methodology set out in the operator's flow monitoring quality " +
            "management system."),
    ];

    // Reuses WqFormParagraphOverlayTests's own font resolver (family/bold/italic-aware, mapping
    // to real macOS Arial/Times New Roman TTFs) rather than a second, narrower one of its own -
    // a naive Arial-only resolver defined directly in this class previously won the "??=" race
    // (this class's name sorts alphabetically before WqFormParagraphOverlayTests's, so its static
    // constructor ran first and registered first, permanently locking out the better one for the
    // whole test run) - confirmed by a real "No font in show/space" error the moment this file
    // started requesting non-Arial or bold/italic fonts (via the newly-added real font/size
    // detection below), which only a resolver that can actually SUPPLY those variants can satisfy.
    static WqFormPageSplitTests()
    {
        GlobalFontSettings.FontResolver ??= new WqFormParagraphOverlayTests.LocalFontResolver();
    }

    [Theory]
    [MemberData(nameof(WqFormParagraphOverlayTests.SampleFiles), MemberType = typeof(WqFormParagraphOverlayTests))]
    public async Task WhenRealWqFormFile_ThenNewConditionIsInsertedWithPageSplit(string sourcePath)
    {
        var filename = Path.GetFileName(sourcePath);
        var comparisonFolder = Path.Combine(AppContext.BaseDirectory, "ComparisonOutput");
        Directory.CreateDirectory(comparisonFolder);

        File.Copy(
            sourcePath,
            Path.Combine(comparisonFolder, $"{Path.GetFileNameWithoutExtension(filename)}-original.pdf"),
            overwrite: true);

        var outputPath = Path.Combine(
            comparisonFolder, $"{Path.GetFileNameWithoutExtension(filename)}-page-split.pdf");

        var (success, message) = await TryInsertNewConditionAsync(sourcePath, outputPath);

        testOutputHelper.WriteLine(success ? $"OK {filename}: {message}" : $"SKIPPED {filename}: {message}");
    }

    /// <summary>
    /// Combines both real-text features investigated this session into one output per sample
    /// file: the 3.1.5 whole-block text replacement (WqFormSpliceAndOverlayTests.SpliceAndOverlay)
    /// followed by the 3.3.x new-condition page-split insertion (<see cref="TryInsertNewConditionAsync"/>),
    /// so a reviewer can see the full picture of a real document instead of two separate, harder
    /// to compare outputs. The replacement runs first and writes an intermediate file (only when a
    /// SpecialTermsWholeBlock match actually exists in this document - most of this session's real
    /// sample files don't have one); the insertion always runs second, against whichever file
    /// resulted, since it targets a completely different clause elsewhere in the document and the
    /// two features never touch the same content.
    /// </summary>
    [Theory]
    [MemberData(nameof(WqFormParagraphOverlayTests.SampleFiles), MemberType = typeof(WqFormParagraphOverlayTests))]
    public async Task WhenRealWqFormFile_ThenReplacementAndInsertionAreCombined(string sourcePath)
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

        var lookupConfiguration = new WALE.ProcessFile.Core.Configuration.LookupConfiguration(
            WqFormLabelConfiguration.GetLabels(),
            [],
            fileService,
            cacheService,
            outputService,
            null!,
            null!,
            WALE.ProcessFile.Core.Constants.GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            skipFileIfMoreThenPages: 100,
            useLockExclusivity: false);

        var (stopExecution, _, matchesResult) = await pdfDataExtractor.GetMatchesAsync(
            filename,
            new DmsFileData { FileId = Guid.NewGuid() },
            lookupConfiguration,
            [filename],
            -1);

        var comparisonFolder = Path.Combine(AppContext.BaseDirectory, "ComparisonOutput");
        Directory.CreateDirectory(comparisonFolder);

        var wholeBlockMatch = !stopExecution
            ? matchesResult?.Matches?.FirstOrDefault(match => match.LabelGroupName == "SpecialTermsWholeBlock")
            : null;

        var workingPath = sourcePath;
        var replaced = false;

        if (wholeBlockMatch?.Text != null && wholeBlockMatch.Text.Count > 0)
        {
            var matchedLines = WqFormParagraphOverlayTests.RemoveAccidentalOutliers(wholeBlockMatch.Text);
            var intermediatePath = Path.Combine(
                comparisonFolder, $"{Path.GetFileNameWithoutExtension(filename)}-intermediate.pdf");

            new WqFormSpliceAndOverlayTests(testOutputHelper).SpliceAndOverlay(sourcePath, intermediatePath, matchedLines);
            workingPath = intermediatePath;
            replaced = true;
        }

        var outputPath = Path.Combine(
            comparisonFolder, $"{Path.GetFileNameWithoutExtension(filename)}-combined.pdf");

        var (success, message) = await TryInsertNewConditionAsync(workingPath, outputPath);
        var prefix = replaced ? "3.1.5 replaced + " : "";

        testOutputHelper.WriteLine(
            success ? $"OK {filename}: {prefix}{message}" : $"SKIPPED {filename}: {prefix}{message}");
    }

    /// <summary>
    /// Finds the highest existing "3.3.N" condition, the heading-level line right after it to cut
    /// at, and the permit number to anchor footer renumbering on, then inserts the new DWF
    /// data-quality condition via <see cref="TrySplitAndInsert"/> - the shared core of both
    /// <see cref="WhenRealWqFormFile_ThenNewConditionIsInsertedWithPageSplit"/> and
    /// <see cref="WhenRealWqFormFile_ThenReplacementAndInsertionAreCombined"/>, which differ only
    /// in which file this runs against (the original source, or an already-3.1.5-replaced
    /// intermediate) and what they do with the result.
    /// </summary>
    private async Task<(bool Success, string Message)> TryInsertNewConditionAsync(string sourcePath, string outputPath)
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
        var noOcrDataExtractorService = new PdfPigNoOcrDataExtractorService();

        var lookupConfiguration = new WALE.ProcessFile.Core.Configuration.LookupConfiguration(
            [],
            [],
            fileService,
            cacheService,
            outputService,
            null!,
            null!,
            WALE.ProcessFile.Core.Constants.GeneralConstants.UnsetRegionCode,
            DateTime.Now,
            skipFileIfMoreThenPages: 100,
            useLockExclusivity: false);

        var pdfDocument = await noOcrDataExtractorService.GetPdfDocumentAsync(
            filename,
            Guid.NewGuid(),
            outputService,
            cacheService,
            documentService,
            docnetAlternativeDocumentService,
            lookupConfiguration,
            -1);

        if (pdfDocument == null)
        {
            return (false, "could not open pdf document.");
        }

        var lines = await noOcrDataExtractorService
            .GetTextLinesFromPdfAndSaveScreenshotsPageTextLinesAndMetadataAsync(
                pdfDocument, cacheService, outputService, -1);

        var highestClauseLine = lines
            .Select(line => (Line: line, Match: ClauseNumberRegex.Match(line.Text)))
            .Where(x => x.Match.Success)
            .OrderByDescending(x => int.Parse(x.Match.Groups[1].Value))
            .Select(x => x.Line)
            .FirstOrDefault();

        if (highestClauseLine == null)
        {
            return (false, "no \"3.3.N\" condition found.");
        }

        var highestClauseNumber = int.Parse(ClauseNumberRegex.Match(highestClauseLine.Text).Groups[1].Value);
        var newClauseNumber = highestClauseNumber + 1;

        // The cut point is the first line, after the highest 3.3.N clause on the same page, that
        // returns to the same left margin the clause number itself started at - lettered
        // sub-items and wrapped continuation lines are always indented further right than that.
        //
        // That margin-return line can itself be the page's own running footer, not a real
        // heading - confirmed on 8 real files (e.g. wq__17160028, wq__w00243): PdfPig's own line
        // grouping occasionally merges a two-line "Permit number" / value footer (or a
        // "Consolidated Permit Number" / "Page N of M" one) into a single reconstructed line
        // whenever their Y bands sit close enough together, and that merged line sits at the
        // same left margin as the clause numbers themselves. The exact same merge already had to
        // be worked around for the reflow cascade's own page-selection lower down in this file -
        // reusing its "contains both Permit and number" fallback here too, since a real clause
        // heading never contains both words together.
        var baseMargin = highestClauseLine.Left;
        var cutLine = lines
            .Where(line => line.PageNumber == highestClauseLine.PageNumber)
            .OrderByDescending(line => line.Top)
            .SkipWhile(line => line.LineNumber != highestClauseLine.LineNumber)
            .Skip(1)
            .FirstOrDefault(line => line.Left >= 0 && line.Left <= baseMargin + 1 && !string.IsNullOrWhiteSpace(line.Text)
                                     && !LooksLikePermitFooterLine(line.Text));

        // The highest 3.3.N clause can be the very last real content on its own page, with the
        // next heading-level line (e.g. "4 Information") being the first line of the FOLLOWING
        // page instead - confirmed on 4 real files (e.g. wq__17160028: clause 3.3.8 ends right at
        // the bottom of page 8, "4 Information" starts page 9). The same base-margin/footer
        // exclusion applies; a fresh page's own topmost real line is simply the first candidate
        // rather than the one after a same-page clause line.
        cutLine ??= lines
            .Where(line => line.PageNumber == highestClauseLine.PageNumber + 1
                            && line.Left >= 0 && line.Left <= baseMargin + 1
                            && !string.IsNullOrWhiteSpace(line.Text)
                            && !LooksLikePermitFooterLine(line.Text))
            .OrderByDescending(line => line.Top)
            .FirstOrDefault();

        if (cutLine == null)
        {
            return (false, $"could not find a heading-level line after 3.3.{highestClauseNumber} to cut at.");
        }

        // The permit number is whatever short alphanumeric line immediately follows a
        // "Permit number" label line (confirmed structure across every real file this session -
        // e.g. "Permit number" / "002671", "Permit number" / "CM0105501", or
        // "Permit number" / "AW1NF/618" - permit numbers in this corpus aren't always pure
        // digits, and can contain "/"). Anchoring on the label rather than guessing a value
        // format directly avoids false-matching some other short alphanumeric token (an NGR grid
        // reference, a monitoring point ID) elsewhere in the document.
        //
        // Some templates' title/cover page lays out "Variation number"/"Permit number"/value
        // labels in a grid whose reading-order reconstruction doesn't put the label immediately
        // before its own value (confirmed on a real file - "Variation number" / "Permit number" /
        // "number", the actual value missing from that reconstruction entirely). Tried skipping
        // page 1 outright to dodge that, but it regressed a different, previously-reliable file
        // (page 1 isn't uniformly the odd one out across this corpus's template variety) - a net
        // loss, reverted. Left as a known, narrow gap rather than chasing a fix that trades one
        // file's success for another's.
        // Doesn't stop at the first "Permit number" occurrence found - a real file (wq__302142)
        // has a perfectly normal running footer but still failed here, because some *other*,
        // earlier "Permit number" mention in the document (e.g. a cover page reference) matched
        // first and didn't have a parseable value immediately after it, so the whole lookup gave
        // up rather than trying the next candidate. Tries every label match in document order
        // until one actually yields a value, instead of trusting the first hit blindly.
        DocumentLine? permitNumberLine = null;

        foreach (var candidateLabel in lines.Where(line => PermitNumberLabelRegex.IsMatch(line.Text.Trim())))
        {
            permitNumberLine = lines
                .Where(line => line.PageNumber == candidateLabel.PageNumber)
                .OrderByDescending(line => line.Top)
                .SkipWhile(line => line.LineNumber != candidateLabel.LineNumber)
                .Skip(1)
                .FirstOrDefault(line => PermitNumberValueRegex.IsMatch(line.Text.Trim()));

            if (permitNumberLine != null)
            {
                break;
            }
        }

        if (permitNumberLine == null)
        {
            return (false, "could not find a permit number to anchor footer renumbering.");
        }

        var permitNumber = permitNumberLine.Text.Trim();

        var result = TrySplitAndInsert(
            sourcePath, outputPath, lines, highestClauseLine, cutLine, newClauseNumber, permitNumber, permitNumberLine);

        return result.Success
            ? (true, $"inserted 3.3.{newClauseNumber} before \"{cutLine.Text.Trim()}\" on page " +
                     $"{cutLine.PageNumber}, {result.PagesRenumbered} page(s) renumbered.")
            : (false, result.SkipReason ?? "unknown reason.");
    }

    private readonly record struct SplitResult(bool Success, string? SkipReason, int PagesRenumbered);

    /// <summary>
    /// Performs the actual content-stream split: finds the cut line's own text within the raw
    /// content stream (reusing <see cref="WqFormSpliceAndOverlayTests.TryRemoveLineOperator"/>'s
    /// encoding-aware matcher purely for its returned index, not to remove anything), splits the
    /// page there, relocates everything from that point onto a brand new page with a fresh
    /// absolute anchor, draws the new clause into the space that frees up, and renumbers every
    /// following page's footer.
    /// </summary>
    /// <summary>
    /// PDF-generator template families identified in this corpus, by content-stream structure
    /// (not visual appearance - two files can look identical and still be built by different
    /// underlying generators/versions with incompatible content-stream conventions).
    /// </summary>
    private enum TemplateFamily
    {
        /// <summary>
        /// WQ__002671 and siblings: one BT/ET per page, footer reached via TD-chain continuation
        /// from a single Tm anchor. The only family this tool currently supports.
        /// </summary>
        SingleBlock,

        /// <summary>
        /// wq__202711 and siblings: footer wrapped in "/Artifact ... /Subtype/Footer BDC ... EMC"
        /// marked content, with a separate BT/ET pair per text fragment (100+ on a typical page)
        /// rather than one covering the whole page. Confirmed structurally distinct via direct
        /// content-stream inspection this session; not supported.
        /// </summary>
        MarkedContentPerFragment,

        /// <summary>Neither known structural signature - genuinely unclassified.</summary>
        Unknown,
    }

    /// <summary>
    /// Classifies a page's content stream by two cheap, reliable structural signals: presence of
    /// "/Artifact" marked-content tagging, and how many separate BT/ET text objects the page uses
    /// (one for the whole page vs. one per fragment). Confirmed to cleanly separate every real
    /// file checked this session - never a fuzzy/visual guess.
    /// </summary>
    private static TemplateFamily DetectTemplateFamily(string pageContent)
    {
        var btCount = Regex.Matches(pageContent, @"\bBT\b").Count;
        var hasArtifactMarkedContent = pageContent.Contains("/Artifact");

        if (hasArtifactMarkedContent && btCount > 10)
        {
            return TemplateFamily.MarkedContentPerFragment;
        }

        // A single-BT page with /Artifact tagging (confirmed on real files) is a distinct
        // generator variant, but structurally the same "SingleBlock" case this tool already
        // handles - the split/relocation logic only touches Tm/TD/text-show operators and treats
        // everything else (including "/Artifact ... BDC"/"EMC" marked-content wrappers) as opaque
        // pass-through text, so /Artifact tagging alone isn't a reason to decline it.
        if (btCount == 1)
        {
            return TemplateFamily.SingleBlock;
        }

        return TemplateFamily.Unknown;
    }

    /// <summary>
    /// Skips a cut index forward past a "trailing artifact" pattern found in this family too,
    /// not just MarkedContentPerFragment - individual paragraphs are wrapped in their own
    /// "/LBody &lt;&lt;/MCID N&gt;&gt; BDC ... EMC" marked-content span even inside a single
    /// page-wide BT. A whitespace-only text-show operator (a lone trailing space) immediately
    /// followed by "EMC" belongs to the *previous* paragraph's own span, not the one the cut
    /// point is meant to land on - cutting mid-way through it strands that EMC in the moved half
    /// with no matching BDC, and leaves the previous span's own BDC behind in the kept half with
    /// no matching EMC ("Mismatched EMC operator", confirmed via pdftotext on real files - all 9
    /// files the mid-chain fix newly resolved, none of the 58 pre-existing "clean boundary"
    /// files, since those always land right at a fresh span's own start already).
    ///
    /// The fix stops right after that EMC, not past the following BDC too - the next span's own
    /// "/LBody &lt;&lt;...&gt;&gt; BDC" is the opening tag for the content actually being moved
    /// (e.g. the heading itself) and has to stay attached to it in the moved half, paired with
    /// its own later EMC. Consuming it here as if it were more of the same artifact was an
    /// earlier, wrong version of this fix - confirmed by a follow-up pdftotext check still
    /// showing "Mismatched EMC operator" (a different, now-orphaned BDC) even after the initial
    /// "Unknown operator 'BDCET'" gluing bug was fixed.
    /// </summary>
    private static int SkipLeadingWhitespaceOnlySpanArtifact(
        string content, int cutIndex, IReadOnlyDictionary<int, string> toUnicodeMap)
    {
        var tjMatch = WqFormSpliceAndOverlayTests.TjOperatorRegex.Match(content, cutIndex);

        if (!tjMatch.Success || tjMatch.Index != cutIndex)
        {
            return cutIndex;
        }

        var decoded = WqFormSpliceAndOverlayTests.ReconstructLiteralText(tjMatch.Value, toUnicodeMap).Trim();

        if (decoded.Length > 0)
        {
            return cutIndex;
        }

        var afterTj = tjMatch.Index + tjMatch.Length;
        var emcMatch = Regex.Match(content[afterTj..], @"\A\s*EMC\b");

        return emcMatch.Success ? afterTj + emcMatch.Length : cutIndex;
    }

    /// <summary>
    /// Cuts a SingleBlock page's own content at an arbitrary line - used by the reflow cascade in
    /// <see cref="TrySplitAndInsert"/> to pull forward as much of a subsequent original page's
    /// content as fits in a relocated page's free space. Deliberately a separate, simplified
    /// implementation rather than a shared refactor of the initial cut's own logic (the ~180
    /// lines in <see cref="TrySplitAndInsert"/> handling that first cut are the result of many
    /// hard-won, narrowly-scoped fixes already verified against the full sample corpus -
    /// refactoring them to serve a second caller risks regressing behaviour that's already been
    /// independently verified, for a saving that doesn't justify the risk in a POC). Skips the
    /// footer-extraction and "does this duplicate the highest clause" steps entirely - neither
    /// applies here: the tail half always stays on the same page it came from (so it keeps its
    /// own footer naturally), and there's no highest-clause label to collide with once past the
    /// first cut.
    /// </summary>
    private static bool TryCutSingleBlockPageAtLine(
        string content,
        string cutAtLineText,
        string permitNumber,
        IReadOnlyDictionary<int, string> toUnicodeMap,
        out string headPart,
        out string tailPartRaw,
        out double headTopY,
        out double tailTopY,
        out int cutIndexUsed,
        out AnchorMatrix? syntheticAnchorForTail)
    {
        headPart = "";
        tailPartRaw = "";
        headTopY = 0;
        tailTopY = 0;
        cutIndexUsed = 0;
        syntheticAnchorForTail = null;

        var (_, found, cutIndexRaw) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            content, cutAtLineText, toUnicodeMap);

        if (!found)
        {
            return false;
        }

        var cutIndex = SkipLeadingWhitespaceOnlySpanArtifact(content, cutIndexRaw, toUnicodeMap);

        if (content.LastIndexOf("BT", cutIndex, StringComparison.Ordinal) < 0)
        {
            return false;
        }

        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

        // The page's own footer isn't reliably positioned last in the content stream - confirmed
        // false on a real file for the MarkedContentPerFragment family (its footer draws first),
        // so the same defensive skip is applied here too rather than assuming SingleBlock always
        // orders it the other way. Also excludes any tagged header for the same reason a real
        // file (wq__as1004501) needed it excluded elsewhere: an invisible, blank-space
        // "/Subtype/Header" pagination element sitting right at the page's own true top margin
        // otherwise looks like the page's first real content. See the identical checks in
        // <see cref="TryCutMarkedContentPageAtLine"/> for the full story.
        var footerSpansForHead = FindTaggedMarkedContentSpans(content, "Footer")
            .Concat(FindTaggedMarkedContentSpans(content, "Header"))
            .ToList();

        // The tagged-span exclusion above only catches a footer/header that's actually wrapped in
        // its own "/Artifact .../Subtype/Footer" marked content - not every document tags it that
        // way (confirmed on wq__302142: its own "Permit number" footer is plain, untagged BT/Tf/Tm
        // text). Locating it directly by its own known text (the same technique already used to
        // split the footer out of the tail/remainder side below) and excluding its own nearest Tm
        // catches that case too - otherwise firstTmInPage silently resolves to the footer's own Y
        // (near the page bottom) instead of the real body content's, corrupting headTopY and, via
        // it, every Tm this fragment goes on to shift by a wildly wrong delta.
        var (_, footerFoundInPage, footerIndexInPage) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            content, permitNumber, toUnicodeMap);
        var footerOwnTmIndex = footerFoundInPage
            ? tmRegex.Matches(content[..footerIndexInPage]).Cast<Match>().LastOrDefault()?.Index
            : null;

        var firstTmInPage = tmRegex.Matches(content)
            .Cast<Match>()
            .FirstOrDefault(m => !footerSpansForHead.Any(span => m.Index >= span.Start && m.Index <= span.End)
                                  && m.Index != footerOwnTmIndex);

        if (firstTmInPage == null)
        {
            return false;
        }

        headTopY = double.Parse(firstTmInPage.Groups[6].Value);

        var headContentRaw = content[..cutIndex];

        // Strip the page's own footer out of the head part entirely rather than letting it ride
        // along: headDelta is computed relative to the real body content's own Y, so shifting the
        // footer's own Tm by that same delta moves it to whatever Y the body ends up at, not
        // where a footer belongs - and even correctly positioned, it would just duplicate the
        // destination page's own existing footer (cascadeDestFooter already covers that).
        if (footerFoundInPage && footerOwnTmIndex.HasValue && footerIndexInPage < headContentRaw.Length)
        {
            var footerBtIndex = headContentRaw.LastIndexOf("BT", footerOwnTmIndex.Value, StringComparison.Ordinal);
            var footerEtIndex = headContentRaw.IndexOf("ET", footerIndexInPage, StringComparison.Ordinal);

            if (footerBtIndex >= 0 && footerEtIndex >= 0)
            {
                headContentRaw = headContentRaw[..footerBtIndex] + headContentRaw[(footerEtIndex + 2)..];
            }
        }

        headPart = headContentRaw + "\nET";
        tailPartRaw = content[cutIndex..];
        cutIndexUsed = cutIndex;

        var immediateTmMatch = Regex.Match(
            tailPartRaw[..Math.Min(tailPartRaw.Length, 200)],
            @"\A\s*(?:/\S+\s*<<[^>]*>>\s*BDC\s*)?(?:/\S+\s+[\d.]+\s+Tf\s*)?" +
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

        if (immediateTmMatch.Success)
        {
            tailTopY = double.Parse(immediateTmMatch.Groups[6].Value);
            return true;
        }

        if (!TryComputeAbsolutePositionAtCutPoint(content, cutIndex, out var tmMatch, out var cutX, out var cutY))
        {
            return false;
        }

        tailTopY = cutY;
        syntheticAnchorForTail = new AnchorMatrix(
            tmMatch.Groups[1].Value, tmMatch.Groups[2].Value, tmMatch.Groups[3].Value, tmMatch.Groups[4].Value, cutX);

        return true;
    }

    /// <summary>
    /// Finds every marked-content span tagged with the given <c>/Subtype</c> (e.g. "Footer" or
    /// "Header") - used to exclude pagination artifacts from "what's the page's own first real
    /// content Tm" searches. Confirmed necessary on a real file (wq__as1004501): it tags an
    /// invisible, blank-space "/Subtype/Header" element right at the page's own true top margin,
    /// alongside its ordinary "/Subtype/Footer" one - excluding only the footer left the header's
    /// own Tm looking like the page's first content, anchoring a relocated fragment's shift by
    /// the wrong amount and silently drawing real body text into the footer's own space (not
    /// caught by pdftotext - a text overlap isn't a structural defect, only a real render shows
    /// it).
    /// </summary>
    private static List<(int Start, int End)> FindTaggedMarkedContentSpans(string content, string subtypeName)
    {
        var spans = new List<(int Start, int End)>();
        var pattern = $@"/\S+\s*<<[^>]*/Subtype\s*/{subtypeName}[^>]*>>\s*BDC";

        foreach (Match tagMatch in Regex.Matches(content, pattern))
        {
            var emcEnd = content.IndexOf("EMC", tagMatch.Index, StringComparison.Ordinal);

            if (emcEnd >= 0)
            {
                spans.Add((tagMatch.Index, emcEnd + "EMC".Length));
            }
        }

        return spans;
    }

    /// <summary>
    /// Extracts a MarkedContentPerFragment page's entire body (its footer stripped out, wherever
    /// it falls) as one shiftable fragment, with no cut at all - used by the reflow cascade in
    /// <see cref="TrySplitAndInsert"/> when a candidate page's whole remaining content already
    /// fits in the space available and nothing further could ever be pulled in after it (the next
    /// source page starts immediately with its own "Schedule N"/"Table SN" boundary, or has no
    /// content at all). Folding the whole page into the previous destination this way - rather
    /// than reserving a trailing scrap "just in case" - avoids stranding a short, awkward-looking
    /// remainder on what would otherwise become a near-empty page; the now fully-drained candidate
    /// page is removed from the document entirely afterward.
    /// </summary>
    private static bool TryExtractWholeMarkedContentPageBody(string content, out string bodyContent, out double topY)
    {
        bodyContent = "";
        topY = 0;

        var footerTagMatch = Regex.Match(content, @"/\S+\s*<<[^>]*/Subtype\s*/Footer[^>]*>>\s*BDC");

        if (footerTagMatch.Success)
        {
            var emcEnd = content.IndexOf("EMC", footerTagMatch.Index, StringComparison.Ordinal);

            if (emcEnd >= 0)
            {
                content = content[..footerTagMatch.Index] + content[(emcEnd + "EMC".Length)..];
            }
        }

        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");
        var headerSpans = FindTaggedMarkedContentSpans(content, "Header");
        var firstTm = tmRegex.Matches(content)
            .Cast<Match>()
            .FirstOrDefault(m => !headerSpans.Any(span => m.Index >= span.Start && m.Index <= span.End));

        if (firstTm == null)
        {
            return false;
        }

        topY = double.Parse(firstTm.Groups[6].Value);
        bodyContent = content;

        return true;
    }

    /// <summary>
    /// The SingleBlock counterpart to <see cref="TryExtractWholeMarkedContentPageBody"/> - strips
    /// this family's own footer (found via the permit number's own literal text, as elsewhere in
    /// this file, then rebalanced the same way the cascade's own SingleBlock footer split already
    /// does) and returns the whole remaining body as one shiftable fragment, no cut.
    /// </summary>
    private static bool TryExtractWholeSingleBlockPageBody(
        string content, string permitNumber, IReadOnlyDictionary<int, string> toUnicodeMap,
        out string bodyContent, out double topY)
    {
        bodyContent = "";
        topY = 0;

        var (_, footerFound, footerIndex) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            content, permitNumber, toUnicodeMap);
        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

        if (footerFound)
        {
            var footerTm = tmRegex.Matches(content[..footerIndex]).Cast<Match>().LastOrDefault();

            // Remove exactly the footer's own Tm and whatever it draws, up to (not including)
            // the next Tm - not the footer's whole BT...ET block, which for this family isn't
            // reliably self-contained: confirmed on wq__302142's own candidate page, the footer
            // and the real body share a single BT, with the footer's Tm/text drawn FIRST and
            // the body's own first Tm following immediately after with no ET in between.
            // Stripping the whole BT...ET (as a self-contained-footer assumption would) deleted
            // the entire real body along with it, leaving no Tm for the search below to find and
            // silently failing this whole-page fold. Cutting only up to the next Tm removes
            // precisely the footer's own draw calls, wherever in the stream they fall, and
            // leaves the body's own BT/Tm/text/ET untouched.
            if (footerTm != null)
            {
                var nextTm = tmRegex.Matches(content, footerTm.Index + footerTm.Length)
                    .Cast<Match>()
                    .FirstOrDefault();
                var removeEnd = nextTm?.Index ?? content.Length;
                content = content[..footerTm.Index] + content[removeEnd..];
            }
        }

        var headerSpansForWhole = FindTaggedMarkedContentSpans(content, "Header");
        var firstTm = tmRegex.Matches(content)
            .Cast<Match>()
            .FirstOrDefault(m => !headerSpansForWhole.Any(span => m.Index >= span.Start && m.Index <= span.End));

        if (firstTm == null)
        {
            return false;
        }

        topY = double.Parse(firstTm.Groups[6].Value);
        bodyContent = content;

        return true;
    }

    /// <summary>
    /// Measures where this document's own body content naturally starts on a normal page - the
    /// Y of its first real Tm, skipping the footer (wherever it falls, before or after) the same
    /// way the cascade's own cut helpers already do. Used to anchor relocated/new content at this
    /// document's own true top margin instead of a fixed guess: a hardcoded constant confirmed
    /// close enough not to look broken across the corpus, but visibly too far down the page for
    /// this specific family - a real file (wq__aw1nf618) measured 775.92 here against a constant
    /// of 729.9, a ~46pt gap the user flagged as a visible, avoidable blank margin above the
    /// relocated "4 Information" heading. Returns null (letting the caller fall back to the
    /// constant) if no usable Tm can be found at all, rather than guessing.
    /// </summary>
    private static double? MeasureNaturalTopY(
        string pageContent, string permitNumber, IReadOnlyDictionary<int, string> toUnicodeMap)
    {
        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");
        // Excludes any tagged header too, not just the footer - an invisible, blank-space
        // "/Subtype/Header" pagination element sitting right at the page's own true top margin
        // otherwise looks like the page's first real content (confirmed necessary on a real file,
        // wq__as1004501 - see FindTaggedMarkedContentSpans's own comment for the full story).
        var footerSpans = FindTaggedMarkedContentSpans(pageContent, "Footer")
            .Concat(FindTaggedMarkedContentSpans(pageContent, "Header"))
            .ToList();

        // SingleBlock has no such tag to find the footer by - its own Tm reset (found instead via
        // the permit number's own literal text, the same anchor used everywhere else in this
        // file) needs excluding directly. Confirmed necessary on a real file (wq__302142): its
        // footer's own absolute Tm happens to be the very FIRST Tm in the page's raw byte order
        // even though it's drawn at the bottom of the page - an untagged "first Tm" search
        // anchored new content ~29pt from the bottom instead of the page's real top margin,
        // silently losing everything that should have followed it (the shift this produced left
        // the destination's own "available space" negative, so the reflow cascade never even
        // started - confirmed via a real render, not pdftotext, which reported no error at all).
        var (_, footerFound, footerIndex) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            pageContent, permitNumber, toUnicodeMap);

        if (footerFound)
        {
            var footerTm = tmRegex.Matches(pageContent[..footerIndex]).Cast<Match>().LastOrDefault();

            if (footerTm != null)
            {
                footerSpans.Add((footerTm.Index, footerTm.Index + footerTm.Length));
            }
        }

        var firstTm = tmRegex.Matches(pageContent)
            .Cast<Match>()
            .FirstOrDefault(m => !footerSpans.Any(span => m.Index >= span.Start && m.Index <= span.End));

        return firstTm != null ? double.Parse(firstTm.Groups[6].Value) : null;
    }

    /// <summary>
    /// Measures where this page's own footer sits in the same Tm-native (raw content-stream)
    /// coordinate space as <see cref="TryComputeAbsolutePositionAtCutPoint"/>'s own return
    /// value - not the PdfPig-Top space <c>footerLabelLine.Top</c> lives in. The two spaces don't
    /// always align: for most files the gap between a line's own Tm-native Y and its PdfPig Top
    /// is small enough not to matter, but confirmed on a real file (wq__as1004501) to be as much
    /// as ~32pt - enough that the reflow cascade's own "how much space is left" arithmetic
    /// (entirely Tm-native, since it's built from <see cref="TryComputeAbsolutePositionAtCutPoint"/>
    /// results) was comparing against a PdfPig-Top-space footer boundary as if the two were
    /// interchangeable, silently overestimating the available space and drawing body text
    /// straight into the footer - not caught by pdftotext (a text overlap isn't a structural
    /// defect), only by a real render.
    /// </summary>
    private static double? MeasureFooterTmY(
        string pageContent, string permitNumber, IReadOnlyDictionary<int, string> toUnicodeMap)
    {
        var (_, footerFound, footerIndex) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            pageContent, permitNumber, toUnicodeMap);

        if (!footerFound)
        {
            return null;
        }

        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");
        var footerTm = tmRegex.Matches(pageContent[..footerIndex]).Cast<Match>().LastOrDefault();

        return footerTm != null ? double.Parse(footerTm.Groups[6].Value) : null;
    }

    /// <summary>
    /// Cuts a MarkedContentPerFragment page's own content at an arbitrary fragment's line - the
    /// MarkedContentPerFragment counterpart to <see cref="TryCutSingleBlockPageAtLine"/>, used by
    /// the same reflow cascade. Simpler than the SingleBlock version: every fragment has its own
    /// BT/Tm pair by construction, so there's no mid-chain case to detect or synthesize an anchor
    /// for - the cut always lands at a real Tm.
    /// </summary>
    private static bool TryCutMarkedContentPageAtLine(
        string content,
        string cutAtLineText,
        IReadOnlyDictionary<int, string> toUnicodeMap,
        out string headPart,
        out string tailPartRaw,
        out double headTopY,
        out double tailTopY,
        out int textCutIndex,
        out string extractedFooterBlock)
    {
        headPart = "";
        tailPartRaw = "";
        headTopY = 0;
        tailTopY = 0;
        textCutIndex = 0;
        extractedFooterBlock = "";

        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

        // This family's own footer isn't reliably positioned last in the content stream -
        // confirmed false on a real file (wq__aw1nf618): its footer draws FIRST, ahead of any
        // body text. Stripped out of `content` entirely up front, rather than handled wherever it
        // happens to land relative to the chosen cut point, and returned separately so the caller
        // can reattach it, unshifted, to whichever fragment ends up staying on this physical
        // page - leaving it in place either duplicated it (shifted, into the pulled-forward head)
        // or dropped this page's own new content down to size with no footer at all, both
        // confirmed via a real visual/text-order check (pdftotext alone reports neither as an
        // error - a duplicated, misplaced footer or a missing page number is still well-formed
        // PDF, just wrong).
        // Matches the WHOLE tag: name, its double-angle-bracket properties dictionary, and the
        // BDC operator itself - not just the "/Subtype/Footer" key inside that dictionary. An
        // earlier version of this cut at the key's own position instead, which left the
        // dictionary's own opening bracket behind with no matching close - poppler then read
        // everything that followed as still being inside that dangling dictionary,
        // misinterpreting ordinary content-stream operators as dictionary keys ("Dictionary key
        // must be a name object", confirmed via pdftotext on a real file, wq__aw1nf618 - not
        // caught by this tool's own success reporting, which only checks that a cut point could
        // be located at all).
        var footerTagMatch = Regex.Match(content, @"/\S+\s*<<[^>]*/Subtype\s*/Footer[^>]*>>\s*BDC");

        if (footerTagMatch.Success)
        {
            var emcEnd = content.IndexOf("EMC", footerTagMatch.Index, StringComparison.Ordinal);

            if (emcEnd >= 0)
            {
                var spanEnd = emcEnd + "EMC".Length;
                var footerBtStart = content.IndexOf("BT", footerTagMatch.Index, StringComparison.Ordinal);
                var footerEtEnd = footerBtStart >= 0
                    ? content.LastIndexOf("ET", spanEnd, StringComparison.Ordinal)
                    : -1;

                if (footerBtStart >= 0 && footerEtEnd >= footerBtStart)
                {
                    extractedFooterBlock = content[footerBtStart..(footerEtEnd + "ET".Length)];
                }

                content = content[..footerTagMatch.Index] + content[spanEnd..];
            }
        }

        var (_, found, cutIndex) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(content, cutAtLineText, toUnicodeMap);
        textCutIndex = cutIndex;

        if (!found)
        {
            return false;
        }

        var btIndex = content.LastIndexOf("BT", cutIndex, StringComparison.Ordinal);

        if (btIndex < 0)
        {
            return false;
        }

        var ownTm = tmRegex.Match(content[btIndex..cutIndex]);

        if (!ownTm.Success)
        {
            return false;
        }

        tailTopY = double.Parse(ownTm.Groups[6].Value);

        // Excludes any tagged header, not just the footer already stripped above - an invisible,
        // blank-space "/Subtype/Header" pagination element sitting right at the page's own true
        // top margin otherwise looks like the page's first real content. Confirmed as the actual
        // root cause of a real overlap (wq__as1004501): headTopY anchored ~32pt off from where
        // this page's real first line sits, silently drawing pulled-forward body text into the
        // footer's own space on the previous page - not caught by pdftotext (a text overlap isn't
        // a structural defect), only by a real render.
        var headerSpans = FindTaggedMarkedContentSpans(content, "Header");
        var firstTm = tmRegex.Matches(content)
            .Cast<Match>()
            .FirstOrDefault(m => !headerSpans.Any(span => m.Index >= span.Start && m.Index <= span.End));

        if (firstTm == null)
        {
            return false;
        }

        headTopY = double.Parse(firstTm.Groups[6].Value);

        var (closedHead, reopenedTail) = RebalanceMarkedContentAt(content[..btIndex], content[btIndex..]);
        headPart = closedHead;
        tailPartRaw = reopenedTail;

        return true;
    }

    /// <summary>
    /// Merges every resource category a relocated content fragment might reference (fonts, color
    /// spaces, extended graphics states, XObjects, patterns, shadings) from its original source
    /// page into whichever page it now lives on - without this, an operator in the fragment
    /// resolves against the destination page's own (unrelated) resource dictionary instead of the
    /// one it was authored against, exactly the "Bad color space"/"No font in show" failure mode
    /// found earlier this session by an independent pdftotext check, not this tool's own success
    /// reporting.
    /// </summary>
    private static void MergeResourcesInto(
        PdfSharp.Pdf.PdfDocument document, PdfSharp.Pdf.PdfPage source, PdfSharp.Pdf.PdfPage destination)
    {
        foreach (var resourceCategory in new[] { "/Font", "/ColorSpace", "/ExtGState", "/XObject", "/Pattern", "/Shading" })
        {
            var sourceDict = source.Resources.Elements.GetDictionary(resourceCategory);

            if (sourceDict == null)
            {
                continue;
            }

            var destDict = destination.Resources.Elements.GetDictionary(resourceCategory);

            if (destDict == null)
            {
                destDict = new PdfSharp.Pdf.PdfDictionary(document);
                destination.Resources.Elements[resourceCategory] = destDict;
            }

            foreach (var key in sourceDict.Elements.Keys)
            {
                destDict.Elements[key] = sourceDict.Elements[key];
            }
        }
    }

    private SplitResult TrySplitAndInsert(
        string sourcePath,
        string outputPath,
        List<DocumentLine> lines,
        DocumentLine highestClauseLine,
        DocumentLine cutLine,
        int newClauseNumber,
        string permitNumber,
        DocumentLine permitNumberLine)
    {
        using var sourceDocument = PdfReader.Open(sourcePath, PdfDocumentOpenMode.Import);
        using var document = new PdfSharp.Pdf.PdfDocument();

        foreach (var sourcePage in sourceDocument.Pages)
        {
            document.AddPage(sourcePage);
        }

        var targetPageIndex = cutLine.PageNumber - 1;
        var targetPage = document.Pages[targetPageIndex];
        var contentDict = targetPage.Contents.Elements.GetDictionary(0);
        var content = Encoding.Latin1.GetString(contentDict.Stream!.UnfilteredValue);
        var toUnicodeMap = WqFormSpliceAndOverlayTests.BuildToUnicodeMap(targetPage);

        // Detect which PDF-generator template family this page belongs to before attempting
        // anything else. Everything in this method (single Tm-anchored relocation, the footer
        // finder/renumberer) assumes the "SingleBlock" family this tool was built and validated
        // against (WQ__002671 and its siblings: one BT/ET per page, footer reached via TD-chain
        // continuation). A second, real family exists in this corpus (confirmed via wq__202711's
        // raw content stream: /Artifact-tagged marked content, a separate BT/ET per text
        // fragment) that this tool doesn't support at all - failing there deep inside footer
        // renumbering produced confusing, inconsistent skip reasons; detecting it up front gives
        // an honest, immediate answer instead.
        var family = DetectTemplateFamily(content);

        if (family == TemplateFamily.Unknown)
        {
            return new SplitResult(false, $"unsupported PDF template family ({family}) - not attempted.", 0);
        }

        var (_, found, cutIndex) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            content, cutLine.Text, toUnicodeMap);

        if (!found)
        {
            return new SplitResult(false, $"could not locate \"{cutLine.Text.Trim()}\" in the raw content stream.", 0);
        }

        var highestClauseNumber = newClauseNumber - 1;

        // A normal "near top of page" position, matching this document's own margin - measured
        // directly from this page's own first real body line (before it's cut) rather than
        // assumed, so relocated/new content starts exactly where this document's own template
        // naturally starts a page, not a fixed guess. Falls back to a constant confirmed close
        // enough not to look broken on files where a natural top can't be measured.
        var NewAnchorY = MeasureNaturalTopY(content, permitNumber, toUnicodeMap) ?? 729.9;
        var tmRegex = new Regex(
            @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

        string keptContent;
        string movedTail;
        double oldAnchorY;
        string? syntheticTm = null;

        if (family == TemplateFamily.SingleBlock)
        {
            // One BT covers the whole page here, so the cut happens mid-block: everything up to
            // the cut line stays (closed off with a synthetic ET), and the moved half starts a
            // brand new BT of its own, anchored by a synthetic Tm computed from the cut point's
            // true absolute position - walking forward from the nearest preceding Tm through
            // every relative TD/Td/T* move between it and the cut point (see
            // TryComputeAbsolutePositionAtCutPoint). This also handles the "clean boundary" case
            // where the cut line follows its own Tm directly - the walk finds no moves in
            // between, so the cut point's position collapses to just that Tm's own Y, same as a
            // dedicated fast path would give.
            //
            // Originally this used a two-path design: a raw-newline heuristic ("does the cut
            // line's own source-formatting line start with just an optional Tf?") decided whether
            // to even attempt a relocation, declining outright otherwise. Dropped after it turned
            // out unreliable in both directions - content-stream newlines are just formatting
            // whitespace, not reliable operator/line boundaries, so files that were genuinely
            // clean Tm boundaries still failed the heuristic and got wrongly declined (the actual
            // real-world composition of this session's ~14-file "mid-chain" skip bucket, found by
            // diffing this fix's before/after results - not one true chained-move file among
            // them). The unified walk below is correct for both cases without needing to classify
            // which one a file is first.
            cutIndex = SkipLeadingWhitespaceOnlySpanArtifact(content, cutIndex, toUnicodeMap);

            if (content.LastIndexOf("BT", cutIndex, StringComparison.Ordinal) < 0)
            {
                return new SplitResult(false, "could not find the enclosing BT for the cut point.", 0);
            }

            // A plain concatenation is only safe when content[..cutIndex] already ends in
            // whitespace, true for a raw cutIndex (it always points at the start of a text-show
            // operator, itself preceded by whitespace/newline formatting). SkipLeadingWhitespaceOnlySpanArtifact
            // can advance cutIndex to sit immediately after a bare "BDC" token instead, with no
            // separating whitespace - confirmed by a real "Unknown operator 'BDCET'" parse error
            // (poppler glued the two tokens into one) before this newline was added.
            keptContent = content[..cutIndex] + "\nET";
            movedTail = content[cutIndex..];

            // If the (possibly artifact-skipped) cut point now sits right at its own fresh
            // absolute Tm - at most preceded by a marked-content tag/BDC and a Tf - that Tm
            // already anchors the moved content correctly and will get shifted along with every
            // other Tm in movedTail by the generic tmRegex.Replace below; no synthetic Tm needed.
            // This is the common outcome once SkipLeadingWhitespaceOnlySpanArtifact fires (it
            // lands cutIndex right before a fresh span's own BDC+Tf+Tm), and also covers the
            // original "clean boundary" case where nothing needed skipping at all.
            //
            // Only fall back to walking the TD/Td/T* chain from an EARLIER Tm (necessarily
            // searching backward, since there's no Tm to be found forward here at all) when the
            // cut point is genuinely mid-span - inside a multi-line paragraph advanced line by
            // line via TD/T* with no per-line Tm reset, the one case a forward search can't
            // resolve. Getting this wrong in the other direction was a real bug, caught only by
            // an independent pdftotext check (a real preceding-Tm search unconditionally applied
            // here matched some much-earlier, unrelated Tm several paragraphs back whenever the
            // cut point's own true Tm sat just *after* it instead of before, silently drawing the
            // new clause on top of existing content - not something structural validation alone
            // catches, confirmed by a visual render check finding it after pdftotext had already
            // gone clean).
            var immediateTmMatch = Regex.Match(
                movedTail[..Math.Min(movedTail.Length, 200)],
                @"\A\s*(?:/\S+\s*<<[^>]*>>\s*BDC\s*)?(?:/\S+\s+[\d.]+\s+Tf\s*)?" +
                @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

            if (immediateTmMatch.Success)
            {
                oldAnchorY = double.Parse(immediateTmMatch.Groups[6].Value);
            }
            else
            {
                if (!TryComputeAbsolutePositionAtCutPoint(content, cutIndex, out var tmMatch, out var cutX, out var cutY))
                {
                    return new SplitResult(false, "could not find the cut point's own Tm to anchor the relocation.", 0);
                }

                oldAnchorY = cutY;

                // oldAnchorY is set to exactly cutY above, so the delta computed below
                // (NewAnchorY - oldAnchorY) collapses this straight to NewAnchorY - the synthetic
                // Tm doesn't need to go through the same tmRegex.Replace shift as everything else
                // in movedTail, it's simplest to just bake the final value in directly. X is left
                // as originally authored, matching how every other Tm in this file is only ever
                // shifted vertically, never horizontally.
                syntheticTm =
                    $"{tmMatch.Groups[1].Value} {tmMatch.Groups[2].Value} {tmMatch.Groups[3].Value} " +
                    $"{tmMatch.Groups[4].Value} {cutX:0.####} {NewAnchorY:0.####} Tm";
            }
        }
        else
        {
            // MarkedContentPerFragment: every fragment (often a single word) has its own
            // BT/Tm/text-show/ET, unlike SingleBlock's one BT for the whole page. Cutting mid-line
            // here (like the SingleBlock branch does) would orphan this fragment's own BT+Tm in
            // the kept half and leave the moved half with no position at all - the correct cut
            // point is the start of the enclosing BT itself, not the text-show operator's own
            // line. Confirmed via direct content-stream inspection (wq__202711): the Tm always
            // sits on its own line immediately after BT, one line before the text-show operator,
            // so a mid-line cut misses it entirely.
            var btIndex = content.LastIndexOf("BT", cutIndex, StringComparison.Ordinal);

            if (btIndex < 0)
            {
                return new SplitResult(false, "could not find the enclosing BT for the cut point.", 0);
            }

            var ownTm = tmRegex.Match(content[btIndex..cutIndex]);

            if (!ownTm.Success)
            {
                return new SplitResult(false, "could not find the cut point's own Tm to anchor the relocation.", 0);
            }

            oldAnchorY = double.Parse(ownTm.Groups[6].Value);

            // Each clause/paragraph is additionally wrapped in its own marked-content span
            // ("/P <</MCID N>> BDC ... EMC", confirmed via wq__202671). Cutting at the fragment's
            // bare BT ignores this: it can fall *inside* a span that opened earlier, leaving that
            // span's BDC behind in the kept half with no matching EMC, and its eventual EMC
            // stranded in the moved half with no matching BDC.
            //
            // An earlier version of this fix tried to avoid that by walking backward to the
            // nearest BDC and cutting there instead, on the assumption that every fragment gets
            // its own dedicated span starting immediately before it. Confirmed false on a real
            // file (wq__202711): headings like "4 Information" are untagged (no span opens for
            // them specifically) - they, and the paragraphs around them, all sit inside one much
            // larger span. There's no nearby BDC to walk back to at all; the backward search
            // (with a proximity check that turned out to be a no-op bug) instead grabbed a BDC
            // over 1000 characters earlier, dragging the highest clause's own final sentence into
            // movedTail with it.
            //
            // The fix here doesn't try to find a natural span boundary at all - it always cuts
            // exactly at the fragment's own BT, then mechanically rebalances whatever spans that
            // leaves open: an EMC for each one appended to the end of keptContent (closing them
            // there) and a fresh synthetic BDC for each one prepended to movedTail (reopening
            // equivalents there). This produces two independently-balanced halves regardless of
            // how the original spans were laid out - poppler doesn't care that the reopened tag
            // isn't the "real" one structurally, only that BDC/EMC nest correctly, and a demo
            // prototype doesn't need perfect accessibility-tree fidelity, just correct rendering.
            // The same "cutting mid-scope" problem applies to graphics-state save/restore, not
            // just marked content - confirmed on a real file (wq__202711, a different sample than
            // the one above): cutting at btIndex left one more "Q" than "q" in the whole document,
            // poppler's "Restoring state when no valid states to pop". A "q ... Q" pair can wrap
            // several fragments' worth of content the same way a marked-content span can; the fix
            // is the identical rebalancing technique applied to a second, independent nesting
            // depth.
            var cutStart = btIndex;
            var openSpanCount = 0;
            var openStateCount = 0;

            // Displayed text routinely contains a bare "q" or "Q" as its own parenthesized run -
            // kerning splits a word like "Quality" into "(Q)3(uality)" for individual glyph
            // positioning, the exact same style used throughout this corpus - so a naive \bq\b
            // search matches those too, not just the real q/Q operators. Confirmed by a real
            // regression: fixing the single file this depth-tracking was built for exposed the
            // same "Restoring state" error on ~20 *more* files, all with real prose somewhere in
            // the counted range. Stripping every "(...)"/"<...>" string-literal run first (the
            // same primitive already used to find text-show operators elsewhere in this file)
            // before searching for bare operator tokens removes that entire class of false match.
            var contentForDepthScan = WqFormSpliceAndOverlayTests.StringRunRegex.Replace(content[..cutStart], "");

            foreach (Match tag in Regex.Matches(contentForDepthScan, @"\bBDC\b|\bEMC\b|\bq\b|\bQ\b"))
            {
                switch (tag.Value)
                {
                    case "BDC": openSpanCount++; break;
                    case "EMC": openSpanCount--; break;
                    case "q": openStateCount++; break;
                    case "Q": openStateCount--; break;
                }
            }

            openSpanCount = Math.Max(0, openSpanCount);
            openStateCount = Math.Max(0, openStateCount);

            keptContent = content[..cutStart]
                + string.Concat(Enumerable.Repeat("\nEMC", openSpanCount))
                + string.Concat(Enumerable.Repeat("\nQ", openStateCount));
            movedTail = string.Concat(Enumerable.Repeat("q\n", openStateCount))
                + string.Concat(Enumerable.Repeat("/Span <</MCID -1>> BDC\n", openSpanCount))
                + content[cutStart..];
        }

        // Sanity check the fundamental assumption every cut above relies on: that movedTail
        // genuinely *starts* right after the highest existing clause, so relocating it can never
        // drag part of that clause along too. Found false on a real file (wq__aw1nf618) via a
        // visual render, not by pdftotext: MarkedContentPerFragment's own backward span search
        // (looking for the nearest "/P <<...>> BDC" before the cut fragment's BT) reached past
        // the true cut point and grabbed the tail of the highest clause's own paragraph too -
        // rendering it directly on top of the freshly inserted new one.
        //
        // Checking specifically whether movedTail's own leading text is the highest clause's own
        // label - not whether the label appears anywhere at all in movedTail - matters: an
        // earlier version of this check flagged the label anywhere in the decoded text and
        // regressed ~35 previously-good files in one run, because later clauses routinely
        // cross-reference earlier ones in running text ("as required by condition 3.3.6") -
        // legitimate content that's correctly part of movedTail and must stay there. Only a
        // recurrence at the very start of movedTail indicates the bug this check exists for.
        var decodedMovedTextStart = string.Concat(
                WqFormSpliceAndOverlayTests.TjOperatorRegex.Matches(movedTail)
                    .Select(match => WqFormSpliceAndOverlayTests.ReconstructLiteralText(match.Value, toUnicodeMap)))
            .TrimStart();

        if (Regex.IsMatch(decodedMovedTextStart, $@"^3\.3\.{highestClauseNumber}\b"))
        {
            return new SplitResult(
                false,
                $"the content that would be relocated to the new page starts with the highest " +
                $"existing clause's own label (\"3.3.{highestClauseNumber}\") - relocating it would " +
                "duplicate or scramble that clause rather than leaving it intact on the original " +
                "page.",
                0);
        }

        // The original page's own trailing footer sits at the end of the SAME content stream as
        // its body text (both families draw it last), which by default puts it inside movedTail,
        // right after the cut point - the split above has no way to know it's special. Left
        // alone, the footer both vanishes from the original (kept) page - it never re-appears
        // anywhere else - and gets dragged onto the new page as an unwanted duplicate, Y-shifted
        // along with everything else and landing just above the new page's own freshly-drawn one.
        // Found via a real visual render, not by pdftotext (a missing or duplicate footer isn't a
        // structural defect it would ever flag): page 9 rendered with no footer at all, page 10
        // rendered with two. Locating the footer by its own permit-number text (already available
        // here) and walking back to its own anchoring Tm splits it out of movedTail and restores
        // it, completely unmodified, to the end of the kept page instead.
        var (_, footerFound, footerMatchIndexInMovedTail) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            movedTail, permitNumber, toUnicodeMap);

        if (footerFound)
        {
            var footerTmInMovedTail = tmRegex.Matches(movedTail[..footerMatchIndexInMovedTail])
                .Cast<Match>()
                .LastOrDefault();

            if (footerTmInMovedTail != null)
            {
                // The footer is commonly wrapped in its own marked-content span too (confirmed on
                // real files: "/Artifact <</.../Subtype /Footer .../>>BDC ... EMC"), the same
                // "/P"/"/LBody" pattern the body text uses elsewhere on the page. Cutting at the
                // Tm alone can split that span in half, and searching backward for "the nearest
                // BDC" to avoid it has the same fundamental problem the body-cut version of that
                // heuristic had (see the MarkedContentPerFragment branch above): when the footer
                // isn't wrapped in its own dedicated span, there's no nearby BDC to find, and
                // walking back to a distant, unrelated one drags body content along with it.
                //
                // The fix here mirrors the body-cut one exactly, roles reversed: always cut at the
                // Tm itself, then rebalance whatever spans/graphics-state that leaves open within
                // movedTail's own [0, cutPoint) range - close them at the end of the shortened
                // movedTail (which stays on the new page) and reopen synthetic equivalents at the
                // start of footerBlock (which moves back to the kept page).
                var footerBlockStart = footerTmInMovedTail.Index;
                var footerScan = WqFormSpliceAndOverlayTests.StringRunRegex.Replace(movedTail[..footerBlockStart], "");
                var footerOpenSpanCount = 0;
                var footerOpenStateCount = 0;

                foreach (Match tag in Regex.Matches(footerScan, @"\bBDC\b|\bEMC\b|\bq\b|\bQ\b"))
                {
                    switch (tag.Value)
                    {
                        case "BDC": footerOpenSpanCount++; break;
                        case "EMC": footerOpenSpanCount--; break;
                        case "q": footerOpenStateCount++; break;
                        case "Q": footerOpenStateCount--; break;
                    }
                }

                footerOpenSpanCount = Math.Max(0, footerOpenSpanCount);
                footerOpenStateCount = Math.Max(0, footerOpenStateCount);

                var footerBlock = string.Concat(Enumerable.Repeat("q\n", footerOpenStateCount))
                    + string.Concat(Enumerable.Repeat("/Span <</MCID -1>> BDC\n", footerOpenSpanCount))
                    + movedTail[footerBlockStart..];
                movedTail = movedTail[..footerBlockStart]
                    + string.Concat(Enumerable.Repeat("\nEMC", footerOpenSpanCount))
                    + string.Concat(Enumerable.Repeat("\nQ", footerOpenStateCount));

                keptContent = family == TemplateFamily.SingleBlock
                    ? keptContent[..^"\nET".Length] + "\n" + footerBlock + "\nET"
                    : keptContent + "\n" + footerBlock;
            }
        }

        var delta = NewAnchorY - oldAnchorY;

        var shiftedTail = tmRegex.Replace(movedTail, match =>
        {
            var y = double.Parse(match.Groups[6].Value) + delta;
            return $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value} " +
                   $"{match.Groups[4].Value} {match.Groups[5].Value} {y:0.####} Tm";
        });

        // The cut point isn't always immediately preceded by its own Tf (only true by luck of
        // structure for some files) - a page can set a font once and keep using it for many lines
        // afterwards via inherited graphics state, and MarkedContentPerFragment's own per-fragment
        // BT often omits Tf entirely, relying on whatever an earlier fragment set. The relocated
        // content starts on a brand new page with no such state to inherit, so whatever font was
        // actually active at the cut point (the nearest PRECEDING Tf anywhere earlier in the
        // stream, not just on this line) has to be carried forward explicitly - otherwise the
        // first Tj in the moved content has no font selected at all ("No font in show", confirmed
        // by an independent pdftotext check - the self-reported "OK" above doesn't catch this,
        // which is exactly why every trial this session has been verified externally rather than
        // trusted at face value). Redundant if the cut point did already start with its own Tf -
        // setting the same
        // font twice is harmless.
        var activeFontMatch = Regex.Matches(content[..cutIndex], @"/(\S+)\s+([\d.]+)\s+Tf")
            .Cast<Match>()
            .LastOrDefault();

        if (activeFontMatch == null)
        {
            return new SplitResult(false, "could not determine the font active at the cut point.", 0);
        }

        // SingleBlock's moved tail is bare (the cut point was mid-line, inside what was one big
        // BT covering the whole page), so it needs a brand new "BT\n<Tf>\n" prefix of its own.
        // MarkedContentPerFragment's moved tail already starts with its own marked-content tag
        // and BT (the cut was at that boundary specifically) - the font just needs inserting
        // right after whichever "BT" starts the moved content (not necessarily position 0, since
        // the tag construct precedes it), not a second BT prepended in front.
        string movedContent;

        if (family == TemplateFamily.SingleBlock)
        {
            // Mid-chain cuts need the synthetic Tm computed above inserted between the Tf and the
            // rest of the (already Y-shifted) moved content - it's the relocated content's only
            // absolute anchor, since a clean-Tm-boundary cut's own original Tm was left behind in
            // the kept half.
            movedContent = syntheticTm != null
                ? $"BT\n{activeFontMatch.Value}\n{syntheticTm}\n{shiftedTail}"
                : $"BT\n{activeFontMatch.Value}\n{shiftedTail}";
        }
        else
        {
            var firstBtInMovedTail = shiftedTail.IndexOf("BT", StringComparison.Ordinal);
            var insertAt = firstBtInMovedTail + "BT".Length;
            movedContent = shiftedTail.Insert(insertAt, $"\n{activeFontMatch.Value}");
        }

        contentDict.Stream.Value = Encoding.Latin1.GetBytes(keptContent);
        contentDict.Elements.Remove("/Filter");

        // Draw the new clause into the vacated space, matching the document's own hanging-indent
        // convention for numbered conditions (label at the base margin, body starting one
        // hanging-indent step further in - the same convention used throughout the DWF POCs).
        // Real text this time, not a placeholder: the DWF data-quality condition transcribed from
        // ~/Downloads/DWF updates NPS guide.pdf, reusing WqFormParagraphOverlayTests.DrawItem so
        // it renders with the same hanging-indent structure as every other real-text POC this
        // session.
        var targetPageHeight = targetPage.Height.Point;

        // Match the document's own font (family, weight, size) instead of a fixed Arial 10pt -
        // the same detection already proven for the 3.1.5 text-replacement feature
        // (WqFormSpliceAndOverlayTests.GetFontInfoAtPosition), reused here rather than
        // reimplemented: reads the nearest preceding Tf's nominal size, scaled by the nearest
        // preceding Tm's own scale factor (Word/LibreOffice-style generators commonly emit a
        // nominal "1 Tf" and bake the real size into the text matrix instead - confirmed on real
        // files including wq__aw1nf618), then resolves the font resource's /BaseFont name to a
        // family/bold/italic triple. Found visually mismatched (both font and size) against a
        // hardcoded Arial 10pt on real files before this fix.
        //
        // Detecting at cutIndex itself was a second bug, not just the hardcoded fallback: cutIndex
        // is the start of "4 Information" - a section HEADING, usually bold and a different size
        // from ordinary clause body text - so the new clause (which should look like an ordinary
        // numbered condition, not a heading) inherited heading styling instead. Confirmed on a
        // real file (wq__302142): the inserted 3.3.8 rendered bold and oversized compared to every
        // real clause around it. Detecting at the highest existing clause's own position instead
        // matches what a real body clause actually looks like in this document, falling back to
        // cutIndex only if that clause's own text can't be located in the content stream at all.
        var (_, highestClauseTextFound, highestClauseTextIndex) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
            content, highestClauseLine.Text, toUnicodeMap);
        var fontDetectionIndex = highestClauseTextFound ? highestClauseTextIndex : cutIndex;
        var fontDictionary = targetPage.Resources.Elements.GetDictionary("/Font");
        var detectedFont = WqFormSpliceAndOverlayTests.GetFontInfoAtPosition(content, fontDetectionIndex, fontDictionary);
        var newClauseFontStyle = (detectedFont.Bold, detectedFont.Italic) switch
        {
            (true, true) => XFontStyleEx.BoldItalic,
            (true, false) => XFontStyleEx.Bold,
            (false, true) => XFontStyleEx.Italic,
            _ => XFontStyleEx.Regular,
        };
        var newClauseFont = new XFont(detectedFont.Family, detectedFont.Size, newClauseFontStyle);

        var leftMargin = highestClauseLine.Left;
        var bodyIndent = leftMargin + 32;

        // The gap between a lettered item's own label ("(a)") and where its body text starts is
        // also a per-document convention, not a fixed 32pt - measured from the nearest real
        // lettered item on the same page (its own label word vs. its own first body word), the
        // same word-position-based technique WqFormParagraphOverlayTests already uses for the
        // 3.1.5 replacement feature. Falls back to 24pt (closer to this corpus's typical value
        // than the old 32pt guess) if no lettered item can be found to measure from at all.
        var referenceItemLine = lines.FirstOrDefault(
            line => line.PageNumber == highestClauseLine.PageNumber
                    && Regex.IsMatch(line.Text.TrimStart(), @"^\([a-z]\)\s+", RegexOptions.IgnoreCase));
        var HangingIndent = 24.0;

        if (referenceItemLine != null)
        {
            var referenceWords = referenceItemLine.Columns
                .SelectMany(column => column.Words)
                .OrderBy(word => word.Coordinates.Left)
                .ToList();

            if (referenceWords.Count >= 2)
            {
                HangingIndent = referenceWords[1].Coordinates.Left - referenceWords[0].Coordinates.Left;
            }
        }

        var rightEdge = targetPage.Width.Point - 60;

        // Measure the whole clause's height before deciding where to draw it - found visually on
        // a real file (wq__aw1nf618) after independent structural checks had already called it
        // "OK": item (d) ran straight through the "Permit number" footer with nothing to stop it,
        // since nothing previously checked available space before drawing. A PDF structural check
        // (pdftotext/stderr) can't catch this at all - it's a legible, well-formed page that's
        // simply wrong to look at, so only measuring against a bound catches it.
        //
        // Measured against a throwaway page in its own standalone document, not targetPage -
        // font metrics don't depend on which page they're measured against, and calling
        // XGraphics.FromPdfPage(targetPage) here as well as later (for the real draw) corrupted
        // every single file's content stream ("Bad block header in flate stream", confirmed via
        // pdftotext on the full batch, not just the 13 files this fallback was built for) -
        // PdfSharp's per-page content-stream bookkeeping doesn't tolerate two separate
        // FromPdfPage sessions against the same page, even when the first one never draws
        // anything before disposing.
        // Measured per item, not just as one combined total - the clause can flow across a page
        // break like a real paginated document (as many whole items as fit stay right after the
        // existing highest clause, the rest continue at the top of a fresh page), rather than an
        // all-or-nothing choice between "fits entirely" and "moves entirely".
        double[] itemHeights;

        using (var measureDocument = new PdfSharp.Pdf.PdfDocument())
        {
            var measurePage = measureDocument.AddPage();
            using var measureGraphics = XGraphics.FromPdfPage(measurePage);

            itemHeights = NewConditionItems
                .Select(item => WqFormParagraphOverlayTests.MeasureItemHeight(
                    measureGraphics, item, newClauseFont, rightEdge - bodyIndent, rightEdge - (bodyIndent + HangingIndent),
                    rightEdge - (bodyIndent + HangingIndent)))
                .ToArray();
        }

        var neededHeight = itemHeights.Sum();

        // Tm-native (raw content-stream), not PdfPig-Top - itemTopOnOldPage below is derived
        // from drawAnchorY, itself Tm-native, and the two spaces don't always align closely
        // enough to compare directly (confirmed on a real file, wq__as1004501, a ~32pt gap - see
        // MeasureFooterTmY's own comment for the full story). A page whose footer can't be found
        // this way falls back to a fixed margin from the page's own physical bottom instead.
        var footerLimit =
            (MeasureFooterTmY(content, permitNumber, toUnicodeMap) is { } footerTmY
                ? targetPageHeight - footerTmY - 10
                : targetPageHeight - 50);

        // oldAnchorY is the cut point's own position - originally wherever "4 Information" (a
        // section HEADING) used to start, carrying that heading's own pre-heading margin. A
        // heading's margin is bigger than the gap between two ordinary numbered conditions, so
        // drawing the new clause starting exactly at oldAnchorY inherited a visibly oversized gap
        // - confirmed on a real file (wq__302142): the space before the inserted 3.3.8 was
        // noticeably larger than the gap between any other pair of consecutive clauses on the
        // page. Measuring the real gap this document actually uses between two ordinary clauses
        // (the highest existing clause's own start vs. the end of whichever clause precedes it)
        // and shrinking oldAnchorY by the difference reproduces that same, correct spacing for
        // the new clause instead - only for where THIS clause draws, not for oldAnchorY itself
        // (still needed unmodified for the moved-content Y-shift math elsewhere).
        var drawAnchorY = oldAnchorY;
        var pageLinesTopDown = lines
            .Where(line => line.PageNumber == highestClauseLine.PageNumber && !string.IsNullOrWhiteSpace(line.Text))
            .OrderByDescending(line => line.Top)
            .ToList();
        var lastLineOfHighestClause = pageLinesTopDown
            .SkipWhile(line => line.LineNumber != highestClauseLine.LineNumber)
            .Skip(1)
            .TakeWhile(line => line.Top > cutLine.Top)
            .LastOrDefault();

        // The tightest gap this document actually uses anywhere between two consecutive clauses
        // on the page (not just the one pair immediately before the highest clause, which can
        // itself be a bit looser than the document's typical spacing) - takes the minimum across
        // every "3.3.N" -> "3.3.N+1" transition found, for the closest match to how tightly this
        // template normally packs clauses together.
        var clauseLinesTopDown = pageLinesTopDown
            .Where(line => ClauseNumberRegex.IsMatch(line.Text))
            .ToList();
        var interClauseGaps = new List<double>();

        for (var clauseIndex = 0; clauseIndex < clauseLinesTopDown.Count - 1; clauseIndex++)
        {
            var thisClauseLine = clauseLinesTopDown[clauseIndex];
            var nextClauseLine = clauseLinesTopDown[clauseIndex + 1];
            var lastLineOfThisClause = pageLinesTopDown
                .SkipWhile(line => line.LineNumber != thisClauseLine.LineNumber)
                .Skip(1)
                .TakeWhile(line => line.Top > nextClauseLine.Top)
                .LastOrDefault();
            var gap = (lastLineOfThisClause ?? thisClauseLine).Top - nextClauseLine.Top;

            if (gap > 0)
            {
                interClauseGaps.Add(gap);
            }
        }

        // Comparing cutLine.Top directly against lastLineOfHighestClause.Top (as an earlier
        // version of this did, via a "currentGap = lastLineOfHighestClause.Top - cutLine.Top"
        // delta) mixes units: cutLine is the "4 Information" HEADING, whose bigger/bolder font
        // has a larger ascent-to-baseline offset than the ordinary body text interClauseGaps are
        // measured from, so that delta overstates the true visual gap. Confirmed on a real file
        // (wq__aw1nf618): shrinking by that overstated amount pushed the new clause's label up
        // far enough to overlap the highest clause's own last line ("practicable."). Instead,
        // target lastLineOfHighestClause.Top - normalGap directly - both terms are body-font
        // "Top" values (drawAnchorY approximates a body-font Top for the new label, since the
        // GetHeight() added when converting it to a drawing baseline below cancels against the
        // GetHeight()-ish ascent the same way it does for real body lines), so this reproduces
        // the tightest normal clause-to-clause spacing without the heading-font contamination.
        if (lastLineOfHighestClause != null && interClauseGaps.Count > 0)
        {
            var normalGap = interClauseGaps.Min();
            var desiredAnchorY = lastLineOfHighestClause.Top - normalGap;

            if (desiredAnchorY > oldAnchorY)
            {
                drawAnchorY = desiredAnchorY;
            }
        }

        // The new clause now flows across the page break like a real paginated document instead
        // of an all-or-nothing "fits entirely" vs. "moves entirely" choice: as many whole items
        // as fit stay right after the existing highest clause on the original page, and the rest
        // continue at the top of a fresh page - never splitting one item's own text mid-way. The
        // relocated content ("4 Information" onward) always starts its own subsequent page, never
        // sharing a page with any part of the new clause, the same way a real section heading
        // always starts fresh.
        //
        // Two earlier versions of this were both confirmed wrong on a real file (wq__aw1nf618):
        // moving the whole clause to its own new page whenever it didn't fit in the vacated space
        // left the relocated content's own (short) fragment stranded alone on a second, near-empty
        // page; combining the clause and the relocated content onto one shared page instead (by
        // pushing the relocated content down) avoided that but still put the whole clause on a new
        // page even when most of it would have fit right after 3.3.8 on the original page, which
        // looked disjointed compared to how a document actually paginates.
        var itemTopOnOldPage = targetPageHeight - drawAnchorY + newClauseFont.GetHeight() * 1.3;
        var labelFitsOnOldPage = itemTopOnOldPage <= footerLimit;
        var itemsOnOldPage = 0;

        if (labelFitsOnOldPage)
        {
            var runningTop = itemTopOnOldPage;

            while (itemsOnOldPage < itemHeights.Length && runningTop + itemHeights[itemsOnOldPage] <= footerLimit)
            {
                runningTop += itemHeights[itemsOnOldPage];
                itemsOnOldPage++;
            }
        }

        var needsContinuationPage = !labelFitsOnOldPage || itemsOnOldPage < NewConditionItems.Length;

        if (labelFitsOnOldPage)
        {
            using var graphics = XGraphics.FromPdfPage(targetPage);
            var formatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };
            var itemTop = itemTopOnOldPage;

            graphics.DrawString(
                $"3.3.{newClauseNumber}", newClauseFont, XBrushes.Black,
                new XPoint(leftMargin, targetPageHeight - drawAnchorY + newClauseFont.GetHeight()));

            for (var i = 0; i < itemsOnOldPage; i++)
            {
                itemTop = WqFormParagraphOverlayTests.DrawItem(
                    graphics, formatter, newClauseFont, bodyIndent, rightEdge, bodyIndent + HangingIndent,
                    HangingIndent, itemTop, NewConditionItems[i]);
            }
        }

        // pagesInserted: 1 for the relocated content's own page (always needed), +1 more only if
        // the clause needs a continuation page for whatever didn't fit on the original page.
        var pagesInserted = needsContinuationPage ? 2 : 1;
        var newPageIndex = targetPageIndex + 1;
        PdfSharp.Pdf.PdfPage movedContentPage;

        // The new page(s)' own printed footer numbers need to continue from THIS page's own
        // actual printed number, not its physical position in the document ("cutLine.PageNumber"
        // - confirmed wrong on a real file, wq__302142: it has unnumbered cover pages before the
        // running count starts, so physical index and printed number differ by a constant offset
        // throughout, the exact same reason the renumbering loop below never assumes one from the
        // other either). Falls back to the physical index only if this page's own footer number
        // genuinely can't be read - it always has one, so this is a last resort, not the norm.
        var targetPagePrintedNumber = TryFindFooterPageNumberOperator(
                content, permitNumber, toUnicodeMap, out var actualTargetPrinted, out _, out _, out _, out _)
            ? actualTargetPrinted
            : cutLine.PageNumber;

        if (needsContinuationPage)
        {
            // Page A: the continuation - either the items that didn't fit on the original page,
            // or (rarer: not even the clause's own number/label line had room there) the whole
            // clause including its number, starting at the top margin.
            var clausePage = document.Pages.Insert(newPageIndex, new PdfSharp.Pdf.PdfPage());
            clausePage.Width = targetPage.Width;
            clausePage.Height = targetPage.Height;

            var clausePrintedNumber = targetPagePrintedNumber + 1;

            using (var graphics = XGraphics.FromPdfPage(clausePage))
            {
                var pageHeight = clausePage.Height.Point;
                var font = new XFont("Arial", 10.67);
                graphics.DrawString("Permit number", font, XBrushes.Black, new XPoint(79.32, pageHeight - 28.92));
                graphics.DrawString(permitNumber, font, XBrushes.Black, new XPoint(79.32, pageHeight - 28.92 + 13.5));
                graphics.DrawString(
                    clausePrintedNumber.ToString(), font, XBrushes.Black,
                    new XPoint(79.32 + 59.83, pageHeight - 28.92 + 13.5));

                var formatter = new XTextFormatter(graphics) { Alignment = XParagraphAlignment.Left };
                double itemTop;

                if (!labelFitsOnOldPage)
                {
                    itemTop = pageHeight - NewAnchorY + newClauseFont.GetHeight() * 1.3;

                    graphics.DrawString(
                        $"3.3.{newClauseNumber}", newClauseFont, XBrushes.Black,
                        new XPoint(leftMargin, pageHeight - NewAnchorY + newClauseFont.GetHeight()));
                }
                else
                {
                    // The label already drew on the original page - its own items just continue
                    // here, starting flush at the top margin with no label to repeat.
                    itemTop = pageHeight - NewAnchorY;
                }

                for (var i = itemsOnOldPage; i < NewConditionItems.Length; i++)
                {
                    itemTop = WqFormParagraphOverlayTests.DrawItem(
                        graphics, formatter, newClauseFont, bodyIndent, rightEdge, bodyIndent + HangingIndent,
                        HangingIndent, itemTop, NewConditionItems[i]);
                }
            }

            // Page B: the relocated content, starting fresh at the top of its own page.
            var contentPage = document.Pages.Insert(newPageIndex + 1, new PdfSharp.Pdf.PdfPage());
            contentPage.Width = targetPage.Width;
            contentPage.Height = targetPage.Height;
            movedContentPage = contentPage;

            var contentPrintedNumber = targetPagePrintedNumber + 2;

            using (var graphics = XGraphics.FromPdfPage(contentPage))
            {
                var pageHeight = contentPage.Height.Point;
                var font = new XFont("Arial", 10.67);
                graphics.DrawString("Permit number", font, XBrushes.Black, new XPoint(79.32, pageHeight - 28.92));
                graphics.DrawString(permitNumber, font, XBrushes.Black, new XPoint(79.32, pageHeight - 28.92 + 13.5));
                graphics.DrawString(
                    contentPrintedNumber.ToString(), font, XBrushes.Black,
                    new XPoint(79.32 + 59.83, pageHeight - 28.92 + 13.5));
            }
        }
        else
        {
            // The whole clause fit on the original page already - the relocated content still
            // always gets its own fresh page (never shares with any part of the new clause).
            var newPage = document.Pages.Insert(newPageIndex, new PdfSharp.Pdf.PdfPage());
            newPage.Width = targetPage.Width;
            newPage.Height = targetPage.Height;
            movedContentPage = newPage;

            var newPrintedNumber = targetPagePrintedNumber + 1;

            // Draw the new page's own footer BEFORE touching its /Font resources at all - this
            // lets PdfSharp register and track its own Arial font resource on this page normally.
            // Sharing the whole /Font dictionary *object* with targetPage up front (rather than
            // merging specific entries in afterwards, as below) made PdfSharp's own per-page
            // font-resource bookkeeping collide with the relocated content's original embedded
            // fonts, silently corrupting one or the other - caught only by an independent
            // pdftotext render check (poppler: "Syntax Error: No font in show"), not by anything
            // in this tool's own success reporting, which is exactly why every trial this session
            // has been verified that way rather than trusted at face value.
            using var graphics = XGraphics.FromPdfPage(newPage);
            var pageHeight = newPage.Height.Point;
            var font = new XFont("Arial", 10.67);
            graphics.DrawString("Permit number", font, XBrushes.Black, new XPoint(79.32, pageHeight - 28.92));
            graphics.DrawString(permitNumber, font, XBrushes.Black, new XPoint(79.32, pageHeight - 28.92 + 13.5));
            graphics.DrawString(
                newPrintedNumber.ToString(), font, XBrushes.Black,
                new XPoint(79.32 + 59.83, pageHeight - 28.92 + 13.5));
        }

        // Now merge in (not replace) the original page's resources - not just fonts, but every
        // category the relocated content might reference (color spaces, extended graphics
        // states, XObjects/images, patterns, shadings) - so its operators still resolve,
        // alongside whatever PdfSharp just registered for its own Arial draw above. Missing this
        // for a category a specific file actually uses is exactly how the font bug above
        // happened; found by the same kind of independent pdftotext check, on a different file,
        // for a different resource category ("Bad color space 'Cs6'") - not something this tool's
        // own success reporting caught either time.
        MergeResourcesInto(document, targetPage, movedContentPage);

        void WriteFinalContent(PdfSharp.Pdf.PdfPage page, string bodyContent, string footerContentToWrite)
        {
            var dict = page.Contents.Elements.GetDictionary(0);
            dict.Stream.Value = Encoding.Latin1.GetBytes(bodyContent + "\n" + footerContentToWrite);
            dict.Elements.Remove("/Filter");
        }

        var movedContentFooter = Encoding.Latin1.GetString(
            movedContentPage.Contents.Elements.GetDictionary(0).Stream!.UnfilteredValue);

        // -- Reflow cascade --
        // The relocated content above only ever carries whatever fit in the excerpt cut from the
        // original page's own tail - everything from that same section that used to continue
        // onto the next (untouched) original page is left exactly where it was, regardless of
        // how much free space the relocated page has below it. Confirmed as a real, visible
        // problem on a real file (wq__aw1nf618): the relocated "4 Information" content filled
        // barely a third of its own page while the very next, completely unmodified page carried
        // the rest of the same section in full - not how a real paginated document looks.
        //
        // Fixed by pulling forward as much of each following original page's own content as fits
        // in the space the previous page has left, cutting only at real line boundaries (using
        // this document's own PdfPig line data to decide how many whole lines fit) and never
        // touching table/schedule content this line-level technique was never validated against.
        // The cascade stops - leaving everything from that point on completely untouched - the
        // moment absorbing another page fails for any reason (an unsupported family, a cut point
        // that can't be located, a "Schedule N"/"Table SN" boundary) or a hard hop cap is
        // reached; either is treated as the natural end of this section's flow, not an error.
        //
        // Deliberately reuses only existing PdfPage objects and never inserts or deletes one:
        // each cascade hop replaces a subsequent original page's own content with its own
        // (Y-shifted) remainder plus whatever it then donates backward to the previous page - the
        // total page count is normally unaffected. The one exception: a candidate whose entire
        // remaining content fits AND has nothing further to gain from continuing (the next source
        // page is itself blocked by a boundary, or empty) is folded in whole and its now-empty
        // page removed outright (see the "nextPageIsBlocked" branch below) - pagesRemoved and
        // removedPrintedNumbers track this so the footer-renumbering loop can adjust its own
        // shift amount for pages that came after a removed one.
        const int MaxCascadeHops = 6;
        var cascadeDestPage = movedContentPage;
        var cascadeDestBody = movedContent;
        var cascadeDestFooter = movedContentFooter;
        var cascadeSourcePageIndex = newPageIndex + pagesInserted;
        var cascadeHops = 0;
        var pagesToRemove = new List<PdfSharp.Pdf.PdfPage>();
        var removedPrintedNumbers = new List<int>();

        if ((family == TemplateFamily.SingleBlock || family == TemplateFamily.MarkedContentPerFragment) &&
            TryComputeAbsolutePositionAtCutPoint(cascadeDestBody, cascadeDestBody.Length, out _, out _, out var initialBottomY))
        {
            var cascadeDestBottomY = initialBottomY;
            var cascadeFooterLimitY =
                (MeasureFooterTmY(content, permitNumber, toUnicodeMap) ?? (targetPageHeight - footerLimit - 10)) + 10;
            var normalLineGap = interClauseGaps.Count > 0 ? interClauseGaps.Min() : 14.0;
            var cascadeTmRegex = new Regex(
                @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

            while (cascadeHops < MaxCascadeHops && cascadeSourcePageIndex < document.Pages.Count)
            {
                var availableSpace = cascadeDestBottomY - cascadeFooterLimitY - normalLineGap;

                if (availableSpace < normalLineGap)
                {
                    break;
                }

                var candidatePage = document.Pages[cascadeSourcePageIndex];
                var candidateContentDict = candidatePage.Contents.Elements.GetDictionary(0);
                var candidateContent = Encoding.Latin1.GetString(candidateContentDict.Stream!.UnfilteredValue);

                if (DetectTemplateFamily(candidateContent) != family)
                {
                    break;
                }

                var candidateOriginalPageNumber = cutLine.PageNumber + 1 + cascadeHops;
                var candidateToUnicodeMap = WqFormSpliceAndOverlayTests.BuildToUnicodeMap(candidatePage);

                // PdfPig's own line-grouping occasionally merges this two-line footer ("Permit
                // number" / "<permitNumber> ... <page number>") into a single row when their Y
                // bands sit close enough together - the merged line's word order comes out
                // interleaved by X position (e.g. "Permit 302142 number 8"), which no longer
                // starts with "Permit number" and so never matches PermitNumberLabelRegex.
                // Left undetected, that merged line is treated as ordinary body content,
                // becomes the candidate's own cut line (it's the page's lowest line), and the
                // actual cut then fails on it - confirmed on wq__302142's own page 11, where
                // this silently broke the cascade one hop short of where it needed to reach.
                // Falling back to "contains Permit, number, and this document's own permit
                // number" catches the merge without loosening the match for any other page.
                var candidateFooterLabelLine = lines.FirstOrDefault(
                    l => l.PageNumber == candidateOriginalPageNumber &&
                         (PermitNumberLabelRegex.IsMatch(l.Text.Trim()) ||
                          (l.Text.Contains("Permit", StringComparison.OrdinalIgnoreCase) &&
                           l.Text.Contains("number", StringComparison.OrdinalIgnoreCase) &&
                           l.Text.Contains(permitNumber, StringComparison.Ordinal))));
                var candidateLinesTopDown = lines
                    .Where(l => l.PageNumber == candidateOriginalPageNumber
                                && !string.IsNullOrWhiteSpace(l.Text)
                                && (candidateFooterLabelLine == null || l.Top > candidateFooterLabelLine.Top))
                    .OrderByDescending(l => l.Top)
                    .ToList();


                if (candidateLinesTopDown.Count == 0)
                {
                    break;
                }

                // Never absorb a "Schedule N - ..."/"Table SN.N ..." HEADING, or anything from
                // that point on - a different layout (tables) this line-level splice was never
                // validated against. Case-sensitive and requires the heading's own punctuation
                // (a dash after "Schedule N", a decimal table number after "Table S") -
                // deliberately not case-insensitive: this document's own ordinary body prose
                // routinely cross-references "schedule 4 table S4.1" mid-sentence (lower-case,
                // no dash), and an earlier, looser version of this regex matched those too,
                // truncating the eligible-line list dozens of lines too early on every candidate
                // page and scrambling the cascade's own output - confirmed on a real file
                // (wq__aw1nf618) via a visual/text-order check, not pdftotext (a truncated-then-
                // rejoined cascade is still structurally well-formed, just wrong).
                var boundaryIndex = candidateLinesTopDown.FindIndex(
                    l => Regex.IsMatch(l.Text.TrimStart(), @"^(Schedule\s+\d+\s*[-–—]|Table\s+S\d+\.\d)"));
                var eligibleLines = boundaryIndex >= 0
                    ? candidateLinesTopDown.Take(boundaryIndex).ToList()
                    : candidateLinesTopDown;

                if (eligibleLines.Count == 0)
                {
                    break;
                }

                var topOfPage = eligibleLines[0].Top;
                var cutAtIndex = eligibleLines.Count;

                for (var i = 0; i < eligibleLines.Count; i++)
                {
                    if (topOfPage - eligibleLines[i].Top > availableSpace)
                    {
                        cutAtIndex = i;
                        break;
                    }
                }

                DocumentLine cutLineForCandidate;

                if (cutAtIndex < eligibleLines.Count)
                {
                    // Not everything eligible fit - cut right before the first line that didn't.
                    if (cutAtIndex <= 0)
                    {
                        break;
                    }

                    cutLineForCandidate = eligibleLines[cutAtIndex];
                }
                else if (boundaryIndex >= 0)
                {
                    // Every eligible line fit, and there's a real boundary (a "Schedule N"/
                    // "Table SN" heading) right after them - cutting exactly there leaves that
                    // boundary and everything after it as a genuine, untouched remainder
                    // starting its own fresh page, so there's no need to hold back one of the
                    // eligible lines "just in case" the way the line below still does when
                    // there's no boundary to fall back on. Confirmed necessary on a real file
                    // (wq__aw1nf618): reserving a line here left a single orphaned sentence alone
                    // on its own page even though the previous page had plenty of room for it.
                    cutLineForCandidate = candidateLinesTopDown[boundaryIndex];
                }
                else
                {
                    // No boundary within this candidate's own content, and the whole thing fit.
                    // If the very next source page starts immediately with its own boundary (or
                    // has no content at all), nothing further will ever be pulled in after this
                    // candidate - so there's no reason to hold anything back from it "for later".
                    // Take its entire body as one fragment (no cut at all) and remove the now
                    // fully-drained page from the document afterward, rather than leaving a
                    // short, stranded remainder behind. Confirmed as what a real file actually
                    // needs (wq__aw1nf618): "case it may be provided by telephone." fit
                    // comfortably in the space already available on the page before it - there
                    // was no reason for it to end up alone on its own page at all.
                    var nextPageOriginalNumber = candidateOriginalPageNumber + 1;
                    var nextPageFirstLine = lines
                        .Where(l => l.PageNumber == nextPageOriginalNumber && !string.IsNullOrWhiteSpace(l.Text))
                        .OrderByDescending(l => l.Top)
                        .FirstOrDefault();
                    var nextPageIsBlocked = nextPageFirstLine == null ||
                        Regex.IsMatch(
                            nextPageFirstLine.Text.TrimStart(), @"^(Schedule\s+\d+\s*[-–—]|Table\s+S\d+\.\d)");

                    if (nextPageIsBlocked)
                    {
                        bool wholeExtracted;
                        string wholeBody;
                        double wholeTopY;

                        if (family == TemplateFamily.SingleBlock)
                        {
                            wholeExtracted = TryExtractWholeSingleBlockPageBody(
                                candidateContent, permitNumber, candidateToUnicodeMap, out wholeBody, out wholeTopY);
                        }
                        else
                        {
                            wholeExtracted = TryExtractWholeMarkedContentPageBody(
                                candidateContent, out wholeBody, out wholeTopY);
                        }

                        var wholeFont = family == TemplateFamily.SingleBlock
                            ? Regex.Match(candidateContent, @"/(\S+)\s+([\d.]+)\s+Tf")
                            : Match.Empty;

                        if (!wholeExtracted || (family == TemplateFamily.SingleBlock && !wholeFont.Success))
                        {
                            break;
                        }

                        // Removing this page changes the footer-renumbering shift for every page
                        // that comes after it - determined from its OWN current footer (the same
                        // way the renumbering loop itself reads every page), not assumed from its
                        // physical position, for the same reason that loop never assumed it
                        // either (a cover-page offset, an independently-numbered appendix, etc.).
                        // A page whose own number can't be found this way is left in place
                        // instead of removed - the renumbering-adjustment bookkeeping below only
                        // works for pages this is actually confirmed for.
                        if (!TryFindFooterPageNumberOperator(
                                candidateContent, permitNumber, candidateToUnicodeMap,
                                out var removedPrintedNumber, out _, out _, out _, out _))
                        {
                            break;
                        }

                        // wholeBody (from TryExtractWholeSingleBlockPageBody) is always the
                        // candidate's own original BT...ET with just the footer's draw calls
                        // excised from wherever they fall inside it - self-contained by
                        // construction, the same reasoning as headFragment above. Wrapping it in
                        // another synthetic BT would nest an extra, unmatched one.
                        var wholeDelta = (cascadeDestBottomY - normalLineGap) - wholeTopY;
                        var wholeFragment =
                            BuildShiftedFragment(wholeBody, wholeDelta, null, null, "", FragmentWrapMode.NoFontNeeded);

                        if (!IsMarkedContentBalanced(wholeFragment))
                        {
                            break;
                        }

                        MergeResourcesInto(document, candidatePage, cascadeDestPage);
                        cascadeDestBody += "\n" + wholeFragment;

                        if (!TryComputeAbsolutePositionAtCutPoint(
                                cascadeDestBody, cascadeDestBody.Length, out _, out _, out var newBottomYWhole))
                        {
                            break;
                        }

                        cascadeDestBottomY = newBottomYWhole;
                        pagesToRemove.Add(candidatePage);
                        removedPrintedNumbers.Add(removedPrintedNumber);
                        cascadeSourcePageIndex++;
                        cascadeHops++;

                        continue;
                    }

                    // The next page has more real content of its own - a reserved line here just
                    // becomes the start of whatever that next hop pulls forward into it too, so
                    // holding back only the final line (rather than folding this whole candidate
                    // in) costs nothing.
                    cutLineForCandidate = eligibleLines[^1];
                }

                string headRaw;
                string tailRaw;
                double headTopY;
                double tailTopY;
                AnchorMatrix? syntheticAnchorForTail = null;
                var cutIndexUsed = -1;
                var remainderBody = "";
                var footerBlockForRemainder = "";

                if (family == TemplateFamily.SingleBlock)
                {
                    if (!TryCutSingleBlockPageAtLine(
                            candidateContent, cutLineForCandidate.Text, permitNumber, candidateToUnicodeMap,
                            out headRaw, out tailRaw, out headTopY, out tailTopY,
                            out cutIndexUsed, out syntheticAnchorForTail))
                    {
                        break;
                    }

                    // Split the footer (always at the very end of tailRaw for this family) out
                    // before shifting - it has to stay exactly where it already is, not move with
                    // the body content above it, the same reasoning as the footer split for the
                    // initial cut above.
                    remainderBody = tailRaw;
                    var (_, footerFoundHere, footerIndexInTail) = WqFormSpliceAndOverlayTests.TryRemoveLineOperator(
                        tailRaw, permitNumber, candidateToUnicodeMap);

                    if (footerFoundHere)
                    {
                        var footerTm = cascadeTmRegex.Matches(tailRaw[..footerIndexInTail]).Cast<Match>().LastOrDefault();

                        if (footerTm != null)
                        {
                            var (closedBody, reopenedFooter) = RebalanceMarkedContentAt(
                                tailRaw[..footerTm.Index], tailRaw[footerTm.Index..]);
                            remainderBody = closedBody + "\nET";
                            footerBlockForRemainder = reopenedFooter;
                        }
                    }
                }
                else
                {
                    // This family's footer isn't reliably at the end of tailRaw - already
                    // stripped out of both halves inside the cut itself (see
                    // TryCutMarkedContentPageAtLine's own comment) and returned separately here.
                    if (!TryCutMarkedContentPageAtLine(
                            candidateContent, cutLineForCandidate.Text, candidateToUnicodeMap,
                            out headRaw, out tailRaw, out headTopY, out tailTopY, out cutIndexUsed,
                            out footerBlockForRemainder))
                    {
                        break;
                    }

                    remainderBody = tailRaw;
                }

                var remainderFont = family == TemplateFamily.SingleBlock
                    ? Regex.Matches(
                            candidateContent[..(cutIndexUsed >= 0 ? cutIndexUsed : candidateContent.Length)],
                            @"/(\S+)\s+([\d.]+)\s+Tf")
                        .Cast<Match>()
                        .LastOrDefault()
                    : Regex.Matches(headRaw, @"/(\S+)\s+([\d.]+)\s+Tf").Cast<Match>().LastOrDefault();
                var headFont = family == TemplateFamily.SingleBlock
                    ? Regex.Match(candidateContent, @"/(\S+)\s+([\d.]+)\s+Tf")
                    : Match.Empty;

                if (family == TemplateFamily.SingleBlock && (!headFont.Success || remainderFont == null))
                {
                    break;
                }

                if (family == TemplateFamily.MarkedContentPerFragment && remainderFont == null)
                {
                    break;
                }

                // Build both resulting fragments before writing anything - validated below before
                // either is committed, so a bad cut never partially lands.
                var headDelta = (cascadeDestBottomY - normalLineGap) - headTopY;

                // headRaw is always content[..cutIndex] + "\nET" (see TryCutSingleBlockPageAtLine),
                // with its own embedded footer already stripped out there - the candidate page's
                // own prefix up to the cut, self-contained by construction (one "ET" appended to
                // close whatever single text object was left open). Wrapping it in another
                // synthetic BT (as WrapWithNewBt does for the genuinely-untethered remainder
                // fragment below) would nest an extra, unmatched BT with no ET of its own - an
                // illegal PDF state IsMarkedContentBalanced correctly rejects, safely stopping the
                // cascade short of where it needed to reach. headRaw never needs the wrap or an
                // injected font, matching the MarkedContentPerFragment family's own already-
                // established NoFontNeeded treatment of its analogous head fragment.
                var headFragment = BuildShiftedFragment(headRaw, headDelta, null, null, "", FragmentWrapMode.NoFontNeeded);
                var remainderDelta = NewAnchorY - tailTopY;
                var remainderFragment = family == TemplateFamily.SingleBlock
                    ? BuildShiftedFragment(
                        remainderBody, remainderDelta, syntheticAnchorForTail != null ? NewAnchorY : null,
                        syntheticAnchorForTail, remainderFont!.Value, FragmentWrapMode.WrapWithNewBt)
                    : BuildShiftedFragment(
                        remainderBody, remainderDelta, null, null, remainderFont!.Value,
                        FragmentWrapMode.InsertFontAfterFirstBt);

                // Each fragment has to be independently self-balanced (BT/ET, BDC/EMC, q/Q net to
                // zero) before it's safe to write anywhere - confirmed necessary on real files
                // (several, not just the one this cascade was built for): a handful of candidate
                // pages have marked-content/graphics-state nesting this cut's depth-counting
                // doesn't model correctly (not narrowed down further - the corpus is too varied to
                // chase every idiosyncrasy), producing an unbalanced fragment that pdftotext
                // reported as "Mismatched EMC operator" or "Restoring state when no valid states
                // to pop" on a handful of files - not caught by this tool's own success reporting,
                // which only checks that a cut point could be located, not that what came out of
                // it is well-formed. Treating an imbalance as a cut failure and stopping the
                // cascade there - leaving every page written so far untouched and everything from
                // here on exactly as it was before this hop - is far safer than shipping a
                // plausible-looking but structurally broken page.
                if (!IsMarkedContentBalanced(headFragment) || !IsMarkedContentBalanced(remainderFragment) ||
                    !IsMarkedContentBalanced(footerBlockForRemainder))
                {
                    break;
                }

                // Append the pulled-forward head onto the current destination and finalize it -
                // it's about to be replaced by the candidate page for the next hop, so it has to
                // be written now, not deferred.
                MergeResourcesInto(document, candidatePage, cascadeDestPage);
                WriteFinalContent(cascadeDestPage, cascadeDestBody + "\n" + headFragment, cascadeDestFooter);

                // The candidate page becomes the next destination, holding its own shifted
                // remainder - not written yet, since a further hop might still append to it.
                cascadeDestPage = candidatePage;
                cascadeDestBody = remainderFragment;
                cascadeDestFooter = footerBlockForRemainder;

                if (!TryComputeAbsolutePositionAtCutPoint(
                        cascadeDestBody, cascadeDestBody.Length, out _, out _, out var newBottomY))
                {
                    break;
                }

                cascadeDestBottomY = newBottomY;
                cascadeSourcePageIndex++;
                cascadeHops++;
            }
        }

        WriteFinalContent(cascadeDestPage, cascadeDestBody, cascadeDestFooter);

        // Actually remove any page the cascade fully drained above, now that nothing further
        // reads from it - deferred until here (rather than removed the moment each was decided)
        // so every index used during the cascade loop itself stayed based on the original,
        // stable page ordering throughout.
        foreach (var pageToRemove in pagesToRemove)
        {
            document.Pages.Remove(pageToRemove);
        }

        // Renumber every subsequent original page's footer by +1 - "Ryan's trick": same-length,
        // in-place digit substitution within the existing text run, touching nothing about
        // position or layout. Anchored on the permit number's own literal text (found dynamically
        // above) immediately preceding the page-number token, exactly as validated on the
        // reference file - never a blind "find any number" search, which could corrupt an
        // unrelated value (a percentage, a table figure) elsewhere on the page.
        //
        // The expected old number used to be assumed from physical page index (index i used to
        // have printed number i). Confirmed wrong on several real files: some documents' footer
        // numbering has a constant offset from physical page index throughout (unnumbered cover/
        // intro pages before the running count starts - wq__302142, wq__aecnf1195, wq__a00199),
        // and at least one document numbers perfectly everywhere except a single schedule/
        // appendix page with its own independent sequence (wq__as1004501: pages 9-16 all matched
        // physical index exactly, only page 17 didn't). Reading the actual current number
        // directly from each page's own footer - rather than assuming what it "should" be from
        // its position in the document - handles both automatically; a page where no genuine
        // number can be found at all is left untouched and skipped rather than aborting the whole
        // file over one odd page.
        var renumbered = 0;

        for (var i = newPageIndex + pagesInserted; i < document.Pages.Count; i++)
        {
            var page = document.Pages[i];
            var pageContentDict = page.Contents.Elements.GetDictionary(0);
            var pageContent = Encoding.Latin1.GetString(pageContentDict.Stream!.UnfilteredValue);
            var pageToUnicodeMap = WqFormSpliceAndOverlayTests.BuildToUnicodeMap(page);

            if (!TryFindFooterPageNumberOperator(
                    pageContent, permitNumber, pageToUnicodeMap,
                    out var actualOld, out var operatorAbsoluteStart, out var operatorText, out var tmX, out var tmY))
            {
                continue;
            }

            // A page removed by the cascade above (see pagesToRemove/removedPrintedNumbers)
            // reduces the effective shift by one for every remaining page that came after it in
            // the original numbering - net "+pagesInserted" pages added, minus however many of
            // those were later removed again before this particular page's own original number.
            var printedNew = actualOld + pagesInserted - removedPrintedNumbers.Count(n => n < actualOld);

            if (actualOld.ToString().Length == printedNew.ToString().Length)
            {
                // Same digit count: "Ryan's trick" - same-length, in-place substitution, no
                // layout change at all.
                var oldDigits = actualOld.ToString();
                var newDigits = printedNew.ToString();
                var digitIndexInOperator = operatorText.IndexOf(oldDigits, StringComparison.Ordinal);

                if (digitIndexInOperator < 0)
                {
                    continue;
                }

                var updatedOperatorText =
                    operatorText[..digitIndexInOperator]
                    + newDigits
                    + operatorText[(digitIndexInOperator + oldDigits.Length)..];

                var newPageContent =
                    pageContent[..operatorAbsoluteStart]
                    + updatedOperatorText
                    + pageContent[(operatorAbsoluteStart + operatorText.Length)..];

                pageContentDict.Stream.Value = Encoding.Latin1.GetBytes(newPageContent);
                pageContentDict.Elements.Remove("/Filter");
            }
            else
            {
                // A 9-to-10-style digit-count change can't be a same-length substitution - excise
                // the old digit(s) from the content stream (same technique as everywhere else
                // this session) and draw the new number via XGraphics instead, at the position
                // read directly from the content stream's own preceding Tm. The page number is
                // always the last, right-most element of its own footer line with nothing after
                // it, so redrawing it doesn't risk overlapping anything else.
                var contentWithoutOldNumber =
                    pageContent[..operatorAbsoluteStart] + pageContent[(operatorAbsoluteStart + operatorText.Length)..];

                pageContentDict.Stream.Value = Encoding.Latin1.GetBytes(contentWithoutOldNumber);
                pageContentDict.Elements.Remove("/Filter");

                // Drawn at the position read directly from the content stream (tmX/tmY above),
                // not from PdfPig word coordinates - see TryFindFooterPageNumberOperator's own
                // summary for why. tmX/tmY are in raw PDF content-stream space (origin
                // bottom-left, Y-up); XGraphics draws top-left/Y-down, so Y is flipped against
                // page height, same reasoning used throughout every overlay in this session.
                using var graphics = XGraphics.FromPdfPage(page);
                var pageHeight = page.Height.Point;
                var font = new XFont("Arial", 10.67);
                graphics.DrawString(
                    printedNew.ToString(), font, XBrushes.Black,
                    new XPoint(tmX, pageHeight - tmY));
            }

            renumbered++;
        }

        document.Save(outputPath);
        return new SplitResult(true, null, renumbered);
    }

    /// <summary>
    /// Finds the page-number token that immediately follows the permit number in a page's footer,
    /// returning the number it actually decodes to plus its own operator's exact position and
    /// text in the content stream, and its own preceding Tm's X/Y (the nearest one before the
    /// operator - every footer fragment, in both template families, sets its own absolute Tm
    /// immediately before its text-show operator).
    ///
    /// Discovers the actual current number rather than requiring it to match a pre-computed
    /// expectation (e.g. "physical page index") - real files break that assumption two distinct
    /// ways: a constant offset from physical index for the whole document (unnumbered cover/intro
    /// pages before the running count starts - wq__302142, wq__aecnf1195, wq__a00199), or a
    /// single schedule/appendix page with its own independent sequence in an otherwise perfectly
    /// numbered document (wq__as1004501). Reading the real value directly handles both without
    /// needing to know which situation a given file is in.
    ///
    /// Reuses <see cref="WqFormSpliceAndOverlayTests.TryRemoveLineOperator"/> purely to locate the
    /// permit number's own operator (it already handles both literal and Identity-H hex text
    /// correctly - no need to re-solve that here), rather than guessing at the page number's exact
    /// TJ/Tj encoding: real files render it as either a plain "(N )Tj" or a kerned
    /// "[(NN)-K( )]TJ", and the permit number itself can be split across several parenthesized
    /// runs within one operator (e.g. "[(00)8(267)8(1)-8( )]TJ") - anchoring on decoded text
    /// rather than a hand-written pattern sidesteps needing to model that format at all.
    /// </summary>
    private static bool TryFindFooterPageNumberOperator(
        string pageContent,
        string permitNumber,
        IReadOnlyDictionary<int, string> toUnicodeMap,
        out int actualOld,
        out int operatorAbsoluteStart,
        out string operatorText,
        out double tmX,
        out double tmY)
    {
        actualOld = 0;
        operatorAbsoluteStart = -1;
        operatorText = string.Empty;
        tmX = 0;
        tmY = 0;

        // A page can mention the permit number more than once - a cross-reference or repeated
        // heading earlier in the body text, then the real running footer at the very end -
        // confirmed on real files (wq__010031, wq__102153) where the *first* occurrence on the
        // page wasn't the footer at all, and the "next operator" search that followed it landed
        // on unrelated body text instead of a page number. Every family confirmed this session
        // draws its footer last, so candidates are tried from the LAST occurrence on the page
        // backward to the first, accepting the first one whose own "next operator" genuinely
        // decodes to a plain integer (the footer's real page number, whatever it is).
        var occurrences = new List<(string AfterRemoval, int MatchIndex, int Offset)>();
        var searchOffset = 0;
        var remaining = pageContent;

        while (true)
        {
            var (afterRemovalCandidate, foundCandidate, matchIndexCandidate) =
                WqFormSpliceAndOverlayTests.TryRemoveLineOperator(remaining, permitNumber, toUnicodeMap);

            if (!foundCandidate)
            {
                break;
            }

            occurrences.Add((afterRemovalCandidate, matchIndexCandidate, searchOffset));

            // Advance past the whole matched region (not just its start) before searching again -
            // otherwise the next iteration can re-find the same occurrence from one character in.
            var matchedLength = remaining.Length - afterRemovalCandidate.Length;
            var advance = matchIndexCandidate + Math.Max(1, matchedLength);
            searchOffset += advance;
            remaining = remaining[advance..];
        }

        if (occurrences.Count == 0)
        {
            return false;
        }

        for (var occurrenceIndex = occurrences.Count - 1; occurrenceIndex >= 0; occurrenceIndex--)
        {
            var (afterRemoval, matchIndex, offset) = occurrences[occurrenceIndex];

            // Everything before matchIndex is untouched by the removal; afterRemoval[matchIndex..]
            // is exactly whatever originally followed this occurrence's own operator. The very
            // next operator isn't always the page number itself:
            //  - MarkedContentPerFragment renders a literal space as its own separate operator
            //    between the permit number and the page number (confirmed via wq__202711's raw
            //    content stream: "[(P)...(r)] TJ" then a whole separate "BT...[( )] TJ...ET"
            //    block, then the page number's own block) - skip forward past any operator that
            //    decodes to nothing but whitespace.
            //  - Some files print "Page N" rather than a bare number (confirmed via wq__aecnf1195,
            //    wq__a00199) - skip forward past a "Page" token too, whichever comes first.
            var tail = afterRemoval[matchIndex..];
            Match? nextOperatorMatch = null;
            var decoded = string.Empty;

            foreach (Match candidate in WqFormSpliceAndOverlayTests.TjOperatorRegex.Matches(tail))
            {
                var candidateDecoded = WqFormSpliceAndOverlayTests.ReconstructLiteralText(candidate.Value, toUnicodeMap).Trim();

                if (candidateDecoded.Length == 0 || candidateDecoded.Equals("Page", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                nextOperatorMatch = candidate;
                decoded = candidateDecoded;
                break;
            }

            if (nextOperatorMatch == null || !Regex.IsMatch(decoded, @"^\d+$"))
            {
                continue;
            }

            actualOld = int.Parse(decoded);
            var removedLength = pageContent.Length - offset - afterRemoval.Length;
            operatorAbsoluteStart = offset + matchIndex + removedLength + nextOperatorMatch.Index;
            operatorText = nextOperatorMatch.Value;
            break;
        }

        if (operatorText.Length == 0)
        {
            return false;
        }

        var precedingTm = Regex.Matches(
                pageContent[..operatorAbsoluteStart],
                @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm")
            .Cast<Match>()
            .LastOrDefault();

        if (precedingTm == null)
        {
            return false;
        }

        tmX = double.Parse(precedingTm.Groups[5].Value);
        tmY = double.Parse(precedingTm.Groups[6].Value);

        // The nearest preceding Tm isn't necessarily *this* operator's own position - in the
        // SingleBlock family, one Tm anchors a whole footer line ("Permit number") and every
        // following element on the same line (the permit number, the page number) is reached via
        // relative TD moves from it, not a fresh absolute reset each time. Found by a real visual
        // defect: the redrawn page number landed directly on top of "Permit number" text, because
        // the code was using the label's own Tm position unmodified. Accumulating every TD/Td
        // between the Tm and this operator gives the operator's true position; ordinary MarkedContentPerFragment
        // fragments have no such TD in between, so accumulation is a no-op there and this stays
        // correct for both families.
        var between = pageContent[(precedingTm.Index + precedingTm.Length)..operatorAbsoluteStart];

        foreach (Match move in Regex.Matches(between, @"([\d.\-]+)\s+([\d.\-]+)\s+(?:TD|Td)\b"))
        {
            tmX += double.Parse(move.Groups[1].Value);
            tmY += double.Parse(move.Groups[2].Value);
        }

        return true;
    }
}
