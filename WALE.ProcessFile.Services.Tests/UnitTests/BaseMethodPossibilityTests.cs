using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Methods;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// <summary>
/// BaseMethod.RestrictToPossibility - the generic "does this captured value match one of the
/// label's expected answers" filter used by every Format="Text" label with a Possibilities
/// list. Covers the "" catch-all fix: a field with genuinely no answer on the page produces
/// zero captured lines, not one line with empty text, so the existing Contains("") check
/// (FirstOrDefault() on an empty list is null) never got a chance to recognise a deliberately
/// blank field as a match - it silently vanished instead of surviving as Blank. Found via the
/// real WR51 corpus reporting DidntMatch instead of Blank for 77% of OtherProvisions and 53%
/// of SpecialConditions.
/// </summary>
public class BaseMethodPossibilityTests
{
    private static DocumentLineColumn Column(string text) =>
        new(text
            .Split(' ')
            .Select(w => new DocumentLineWord(w, null, DocumentLineWordCoordinates.NotKnown(), null))
            .ToList());

    private static DocumentLine LineOf(string text) =>
        new(0, 1, [Column(text)], 0, 0, 0, 0);

    private static FunctionInputModel Request(params string[] possibilities) => new()
    {
        label = new LabelToMatch
        {
            Possibilities = possibilities.Select(p => new TextToMatch(p)).ToList()
        }
    };

    private static FunctionInputModel RequestExceptWhenInsideWord(params string[] possibilities) => new()
    {
        label = new LabelToMatch
        {
            Possibilities = possibilities
                .Select(p => new TextToMatch(p) { ExceptWhenInsideWord = true })
                .ToList()
        }
    };

    [Fact]
    public void WhenNoPossibilitiesSet_ThenReturnsResultUnchanged()
    {
        var request = new FunctionInputModel { label = new LabelToMatch() };
        var result = new LabelGroupResult { Text = null };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.Same(result, restricted);
    }

    [Fact]
    public void WhenTextMatchesAPossibility_ThenExtractsJustThatPossibility()
    {
        var request = Request("N/A", "In", "Not");
        var result = new LabelGroupResult { Text = [LineOf("N/A")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
        Assert.Equal("N/A", restricted.Text?.Single().Text);
    }

    [Fact]
    public void WhenTextIsNull_AndPossibilitiesIncludeEmptyStringCatchAll_ThenReturnsMatchWithEmptyText()
    {
        // The exact fix: a genuinely blank field (no captured lines at all) must still count
        // as a match when "" is one of the label's Possibilities - not vanish entirely.
        var request = Request("N/A", "Not", "In", "");
        var result = new LabelGroupResult { Text = null };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
        Assert.NotNull(restricted.Text);
        Assert.Empty(restricted.Text);
    }

    [Fact]
    public void WhenTextIsAnEmptyList_AndPossibilitiesIncludeEmptyStringCatchAll_ThenReturnsMatchWithEmptyText()
    {
        var request = Request("N/A", "Not", "In", "");
        var result = new LabelGroupResult { Text = [] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
        Assert.NotNull(restricted.Text);
        Assert.Empty(restricted.Text);
    }

    [Fact]
    public void WhenTextIsNull_AndPossibilitiesDoNotIncludeEmptyStringCatchAll_ThenReturnsNull()
    {
        // Regression guard: this must stay scoped to labels that explicitly opted into "" as
        // a valid answer (e.g. GetInOrderField) - not become a general "empty text always
        // matches" rule for every Possibilities-restricted label (e.g. the checkbox-mark
        // fields, which have no "" entry and must keep failing to match when blank).
        var request = Request("Y", "N", "X", "x");
        var result = new LabelGroupResult { Text = null };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.Null(restricted);
    }

    [Fact]
    public void WhenTextDoesNotMatchAnyPossibility_AndNoEmptyStringCatchAll_ThenReturnsNull()
    {
        var request = Request("N/A", "In", "Not");
        var result = new LabelGroupResult { Text = [LineOf("Garbage")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.Null(restricted);
    }

    [Fact]
    public void WhenTextIsOneLineWithEmptyString_ThenMatchesViaThePrimaryPathNotTheFallback()
    {
        // Pre-existing behaviour: FirstOrDefault() finds a real (if empty) line here, so the
        // original Contains("") check already handles this - confirms the new fallback isn't
        // needed (or reached) for this shape, only for the genuinely-zero-lines case.
        var request = Request("N/A", "In", "Not", "");
        var result = new LabelGroupResult { Text = [LineOf("")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
        Assert.NotNull(restricted.Text);
        Assert.Single(restricted.Text);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsEmbeddedInALongerWord_ThenDoesNotMatch()
    {
        // The exact real bug: "Point of abstraction:" was wrongly captured (a separate,
        // unrelated column-matching bug) as SourceOfSupply's value, and coincidentally
        // contains "in" inside "Point" - which a plain Contains check accepted as if it were
        // a genuine "In Order" answer.
        var request = RequestExceptWhenInsideWord("N/A", "Not", "In");
        var result = new LabelGroupResult { Text = [LineOf("Point of abstraction:")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.Null(restricted);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsWhitespaceDelimited_ThenStillMatches()
    {
        var request = RequestExceptWhenInsideWord("N/A", "Not", "In");
        var result = new LabelGroupResult { Text = [LineOf("Source of supply: In")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
        Assert.Equal("In", restricted.Text?.Single().Text);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityAppearsBothEmbeddedAndStandalone_ThenStillCountsAsAMatch()
    {
        // "abstraction" contains "n" (as does most English text) but this field also genuinely
        // has a standalone "N" later - MatchesPossibility must not stop searching at the first
        // (embedded, invalid) occurrence and wrongly reject the whole match.
        //
        // Known separate limitation, not fixed here: which WORD gets extracted as "the answer"
        // is decided afterwards by DocumentLineColumn.FilterWordsFromText, a different, widely-
        // shared function with its own independent (and boundary-unaware) Contains-based word
        // search - it can still pick "abstraction" over the real standalone "N" when a text
        // like this has both. That's a pre-existing gap in a much more broadly-used function,
        // out of scope for this fix - it doesn't affect the actual bug this fix targets (there,
        // no standalone occurrence ever exists, so MatchesPossibility correctly rejects the
        // whole match and extraction never runs at all). This test only asserts that a match is
        // found, not which word is extracted.
        var request = RequestExceptWhenInsideWord("N");
        var result = new LabelGroupResult { Text = [LineOf("Means of abstraction: N")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsGluedToPunctuation_ThenStillMatches()
    {
        // Boundary is letter/digit adjacency, not whitespace adjacency - punctuation (a colon,
        // a full stop) counts as a valid boundary either side, only another letter or digit
        // counts as "inside a word". A value glued directly to trailing punctuation with no
        // space (very common - see the real regression below) must still be recognised.
        var request = RequestExceptWhenInsideWord("Y");
        var result = new LabelGroupResult { Text = [LineOf("Y:")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsGluedToAnotherLetter_ThenDoesNotMatch()
    {
        var request = RequestExceptWhenInsideWord("X");
        var result = new LabelGroupResult { Text = [LineOf("Context")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.Null(restricted);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndLabelsOwnColonIsGluedDirectlyToTheValue_ThenStillMatches()
    {
        // The regression this exact scoping caught before it shipped: several real WR51
        // fixtures render a field's own label and value with no space at all -
        // "Source of supply:In Order" is a single PDF word token "supply:In" - so the answer
        // sits immediately after the label's own trailing colon, not whitespace-separated.
        // An earlier version of this fix used whitespace adjacency (matching
        // DataHelper.RemoveExcludes' existing semantics) and wrongly rejected this.
        var request = RequestExceptWhenInsideWord("In");
        var result = new LabelGroupResult { Text = [LineOf("supply:In Order")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
    }

    [Fact]
    public void WhenNotExceptWhenInsideWord_ThenEmbeddedMatchStillCountsAsBefore()
    {
        // Confirms the fix is opt-in per-possibility - existing behaviour (e.g.
        // CheckboxMarkPossibilities, which has no ExceptWhenInsideWord entries) is untouched.
        var request = Request("In");
        var result = new LabelGroupResult { Text = [LineOf("Point of abstraction:")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
    }

    [Fact]
    public void WhenValueIsAStandaloneTickMarkAtTheEndOfTheLine_ThenExtractsIt()
    {
        // End-to-end regression test for the FormattingHelper.TrimFormatting fix (checkbox
        // glyphs are char.IsSymbol and were being trimmed away as the last "word" of the
        // line before this fix - see FormattingHelperTrimFormattingTests in
        // WALE.ProcessFile.Core.Tests). Goes through the real extraction path
        // (DocumentLineColumn.FilterWordsFromText), not just TrimFormatting in isolation, so a
        // regression in either place would be caught here.
        var request = RequestExceptWhenInsideWord("✓");
        var result = new LabelGroupResult { Text = [LineOf("Source of supply: ✓")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result);

        Assert.NotNull(restricted);
        Assert.Equal("✓", restricted.Text?.Single().Text);
    }
}
