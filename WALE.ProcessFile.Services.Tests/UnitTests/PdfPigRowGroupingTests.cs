using WALE.ProcessFile.Services.PdfPig;
using WALE.ProcessFile.Services.PdfPig.Models;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// Row-grouping is the first step of turning a page's raw words into DocumentLines - get it
/// wrong and every label rule downstream is working from corrupted input, with no way to
/// tell from the label-matching side. These exercise the two algorithms directly against
/// synthetic word coordinates, without a PDF or the rest of the extraction pipeline.
/// </summary>
public class PdfPigRowGroupingTests
{
    private static MinimalWord Word(string text, double bottom, double left) =>
        new()
        {
            Text = text,
            BoundingBox = new MinimalPdfRectangle
            {
                Bottom = bottom,
                Top = bottom + 10,
                Left = left,
                Right = left + (text.Length * 6),
                CentroidX = left + (text.Length * 3)
            }
        };

    /// <summary>
    /// Real-world shape of the bug this exists to catch: on the WR51 corpus, "Inspection
    /// Date:" and its neighbouring fields sit ~5pt apart on one row (font-baseline jitter),
    /// while the date value directly beneath the label is ~5pt further down again - so the
    /// gap from row to row is individually smaller than lineHeight even though the total
    /// span across three "rows" is not. A chain-merge algorithm (compare each word only to
    /// the one before it) drags the value into the label's row; anchoring every comparison
    /// to the row's first word does not.
    /// </summary>
    private static List<MinimalWord> LabelWithValueBelowJitteryNeighbour()
    {
        return
        [
            Word("Label", bottom: 100, left: 0),      // row anchor
            Word("Neighbour", bottom: 95, left: 50),   // same row - 5pt jitter from anchor
            Word("Value", bottom: 90, left: 0)         // different row - directly below Label
        ];
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_SeparatesValueRow_WhenChainOfSmallGapsSpansMoreThanLineHeight()
    {
        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor(LabelWithValueBelowJitteryNeighbour(), lineHeight: 6)
            .ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal(["Label", "Neighbour"], rows[0].Select(w => w.Text));
        Assert.Equal(["Value"], rows[1].Select(w => w.Text));
    }

    [Fact]
    public void GroupWordsIntoRowsByChain_MergesValueIntoLabelRow_EvenThoughItIsADifferentVisualRow()
    {
        // Documents the pre-existing behaviour that the licence pipeline still relies on
        // (LookupConfiguration.UseAnchoredLineGrouping defaults to false) - a change here is
        // a deliberate, visible decision, not a silent regression.
        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByChain(LabelWithValueBelowJitteryNeighbour(), lineHeight: 6)
            .ToList();

        var singleRow = Assert.Single(rows);
        Assert.Equal(["Label", "Neighbour", "Value"], singleRow.Select(w => w.Text));
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_KeepsGenuinelySameRowWordsTogether()
    {
        var words = new List<MinimalWord>
        {
            Word("Inspecting", bottom: 100, left: 0),
            Word("Officer:", bottom: 100, left: 60),
            Word("Paul", bottom: 100, left: 110)
        };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor(words, lineHeight: 6)
            .ToList();

        var singleRow = Assert.Single(rows);
        Assert.Equal(["Inspecting", "Officer:", "Paul"], singleRow.Select(w => w.Text));
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_ReturnsNoRows_ForEmptyInput()
    {
        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor([], lineHeight: 6)
            .ToList();

        Assert.Empty(rows);
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_SingleWord_IsItsOwnRow()
    {
        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor([Word("Solo", bottom: 100, left: 0)], lineHeight: 6)
            .ToList();

        var singleRow = Assert.Single(rows);
        Assert.Equal(["Solo"], singleRow.Select(w => w.Text));
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_SeparatesThreeGenuinelyDistinctRows()
    {
        var words = new List<MinimalWord>
        {
            Word("RowOne", bottom: 100, left: 0),
            Word("RowTwo", bottom: 80, left: 0),
            Word("RowThree", bottom: 60, left: 0)
        };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor(words, lineHeight: 6)
            .ToList();

        Assert.Equal(3, rows.Count);
        Assert.Equal("RowOne", rows[0].Single().Text);
        Assert.Equal("RowTwo", rows[1].Single().Text);
        Assert.Equal("RowThree", rows[2].Single().Text);
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_SplitsRow_ExactlyAtLineHeightBoundary()
    {
        // The check is "gap >= lineHeight", so a gap of exactly lineHeight must split, not merge.
        var words = new List<MinimalWord> { Word("Top", bottom: 100, left: 0), Word("Bottom", bottom: 94, left: 0) };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor(words, lineHeight: 6)
            .ToList();

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_MergesRow_JustUnderLineHeightBoundary()
    {
        var words = new List<MinimalWord> { Word("Top", bottom: 100, left: 0), Word("Bottom", bottom: 94.1, left: 0) };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor(words, lineHeight: 6)
            .ToList();

        Assert.Single(rows);
    }

    [Fact]
    public void GroupWordsIntoRowsByAnchor_IsOrderIndependent_RowsComeOutTopToBottomRegardlessOfInputOrder()
    {
        var wordsInReadingOrder = new List<MinimalWord>
        {
            Word("Top", bottom: 100, left: 0),
            Word("Bottom", bottom: 50, left: 0)
        };

        var shuffled = new List<MinimalWord> { wordsInReadingOrder[1], wordsInReadingOrder[0] };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByAnchor(shuffled, lineHeight: 6)
            .ToList();

        Assert.Equal(2, rows.Count);
        Assert.Equal("Top", rows[0].Single().Text);
        Assert.Equal("Bottom", rows[1].Single().Text);
    }

    [Fact]
    public void GroupWordsIntoRowsByChain_StillSplitsOnAGenuinelyLargeSingleGap()
    {
        // The bug is specifically about a CHAIN of small sub-threshold gaps. A single gap
        // well over lineHeight must still split normally under the old algorithm too.
        var words = new List<MinimalWord> { Word("Top", bottom: 100, left: 0), Word("Bottom", bottom: 50, left: 0) };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByChain(words, lineHeight: 6)
            .ToList();

        Assert.Equal(2, rows.Count);
    }

    [Fact]
    public void GroupWordsIntoRowsByChain_MergesSimpleSameRowJitter_LikeAnchorDoes()
    {
        // For the common, non-pathological case both algorithms should agree - this is not
        // a story about anchor being "better" in general, only about the specific
        // transitive-chain shape the bug depends on. Deliberately avoids p/q/y in the words
        // here - the chain algorithm's below-the-line-character compensation (see
        // GroupWordsIntoRowsByAnchor_KeepsGenuinelySameRowWordsTogether's sibling test file
        // comment) can otherwise reorder words with identical raw Bottom values, which would
        // muddy what this test is actually checking.
        var words = new List<MinimalWord>
        {
            Word("Meter", bottom: 100, left: 0),
            Word("Serial", bottom: 100, left: 60),
            Word("Number", bottom: 100, left: 120)
        };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByChain(words, lineHeight: 6)
            .ToList();

        var singleRow = Assert.Single(rows);
        Assert.Equal(["Meter", "Serial", "Number"], singleRow.Select(w => w.Text));
    }

    [Fact]
    public void GroupWordsIntoRowsByChain_CanReorderWordsWithinARow_WhenOneContainsADescenderCharacter()
    {
        // Documents a real, pre-existing quirk (not something this session introduced): the
        // chain algorithm's LineSnappingHelper.CompensateForBelowTheLineCharactersOffset
        // treats any word containing p/q/y as sitting ~1pt lower, purely to correct noisy
        // OCR bounding boxes. For words that otherwise share an identical raw Bottom, that
        // shifts the descender-containing word into a different rounded bucket and it can
        // sort AFTER words that were textually to its right - "Inspecting" ends up last here
        // despite being leftmost, because it contains a 'p'. The anchor algorithm (used only
        // by WR51) has no such compensation - see PdfPigNoOcrDataExtractorService.
        var words = new List<MinimalWord>
        {
            Word("Inspecting", bottom: 100, left: 0),
            Word("Officer:", bottom: 100, left: 60),
            Word("Paul", bottom: 100, left: 110)
        };

        var rows = PdfPigNoOcrDataExtractorService
            .GroupWordsIntoRowsByChain(words, lineHeight: 6)
            .ToList();

        var singleRow = Assert.Single(rows);
        Assert.Equal(["Officer:", "Paul", "Inspecting"], singleRow.Select(w => w.Text));
    }
}
