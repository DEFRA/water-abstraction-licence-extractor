using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Methods;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

/// A field with no answer on the page produces zero captured lines, not one empty line, so a
/// Contains("") check never sees it and a deliberately blank field vanishes instead of surviving
/// as Blank. It read DidntMatch for 77% of OtherProvisions and 53% of SpecialConditions.
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
            Possibilities = possibilities.Select(p => new TextToMatch(p) { ExceptWhenInsideWord = true}).ToList()
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
    public void WhenNoPossibilitiesSet_ThenReturnsNull()
    {
        var request = new FunctionInputModel { label = new LabelToMatch() };
        var restricted = BaseMethod.RestrictToPossibility(request, []);

        Assert.Null(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenTextMatchesAPossibility_ThenExtractsJustThatPossibility()
    {
        var request = Request("N/A", "In", "Not");
        var result = new LabelGroupResult { Text = [LineOf("N/A")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
        Assert.Equal("N/A", restricted.LabelGroupResult.Text);
    }

    [Fact]
    public void WhenTextIsNull_AndPossibilitiesIncludeEmptyStringCatchAll_ThenReturnsMatchWithEmptyText()
    {
        // No captured lines at all must still match when "" is one of the Possibilities.
        var request = Request("N/A", "Not", "In", "");
        var result = new LabelGroupResult { Text = null };

        var restricted = BaseMethod.RestrictToPossibility(request, []);

        Assert.NotNull(restricted.LabelGroupResult);
        Assert.NotNull(restricted.LabelGroupResult.Text);
        Assert.Empty(restricted.LabelGroupResult.Text);
    }

    [Fact]
    public void WhenTextIsAnEmptyList_AndPossibilitiesIncludeEmptyStringCatchAll_ThenReturnsMatchWithEmptyText()
    {
        var request = Request("N/A", "Not", "In", "");
        var result = new LabelGroupResult { Text = [] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
        Assert.NotNull(restricted.LabelGroupResult.Text);
        Assert.Empty(restricted.LabelGroupResult.Text);
    }

    [Fact]
    public void WhenTextIsNull_AndPossibilitiesDoNotIncludeEmptyStringCatchAll_ThenReturnsNull()
    {
        // Scoped to labels that opted into "": the checkbox-mark fields have no "" entry and must
        // keep failing to match when blank.
        var request = Request("Y", "N", "X", "x");
        var result = new LabelGroupResult { Text = null };

        var restricted = BaseMethod.RestrictToPossibility(request, []);

        Assert.Null(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenTextDoesNotMatchAnyPossibility_AndNoEmptyStringCatchAll_ThenReturnsNull()
    {
        var request = Request("N/A", "In", "Not");
        var result = new LabelGroupResult { Text = [LineOf("Garbage")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.Null(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenTextIsOneLineWithEmptyString_ThenMatchesViaThePrimaryPathNotTheFallback()
    {
        // One empty line is already handled by Contains(""); the fallback is for zero lines.
        var request = Request("N/A", "In", "Not", "");
        var result = new LabelGroupResult { Text = [LineOf("")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
        Assert.NotNull(restricted.LabelGroupResult.Text);
        Assert.Equal(string.Empty, restricted.LabelGroupResult.Text);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsEmbeddedInALongerWord_ThenDoesNotMatch()
    {
        // "Point of abstraction:" landed in SourceOfSupply by a separate bug, and a plain Contains
        // took the "in" inside "Point" for an "In Order" answer.
        var request = RequestExceptWhenInsideWord("N/A", "Not", "In");
        var result = new LabelGroupResult { Text = [LineOf("Point of abstraction:")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.Null(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsWhitespaceDelimited_ThenStillMatches()
    {
        var request = RequestExceptWhenInsideWord("N/A", "Not", "In");
        var result = new LabelGroupResult { Text = [LineOf("Source of supply: In")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
        Assert.Equal("In", restricted.LabelGroupResult.Text);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityAppearsBothEmbeddedAndStandalone_ThenStillCountsAsAMatch()
    {
        // "abstraction" contains an "n" and this field has a standalone "N" later, so the search
        // must not stop at the embedded one. Which word is then extracted is decided by
        // FilterWordsFromText, which is boundary-unaware and can still pick "abstraction" - a gap
        // left open, so this asserts only that a match is found.
        var request = RequestExceptWhenInsideWord("N");
        var result = new LabelGroupResult { Text = [LineOf("Means of abstraction: N")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsGluedToPunctuation_ThenStillMatches()
    {
        // The boundary is letter/digit adjacency, not whitespace: punctuation either side counts
        // as a boundary, so a value glued to a trailing colon is still recognised.
        var request = RequestExceptWhenInsideWord("Y");
        var result = new LabelGroupResult { Text = [LineOf("Y:")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndPossibilityIsGluedToAnotherLetter_ThenDoesNotMatch()
    {
        var request = RequestExceptWhenInsideWord("X");
        var result = new LabelGroupResult { Text = [LineOf("Context")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.Null(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenExceptWhenInsideWord_AndLabelsOwnColonIsGluedDirectlyToTheValue_ThenStillMatches()
    {
        // "Source of supply:In Order" is the single token "supply:In", so the answer sits against
        // the colon. An earlier version used whitespace adjacency and rejected it.
        var request = RequestExceptWhenInsideWord("In");
        var result = new LabelGroupResult { Text = [LineOf("supply:In Order")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenNotExceptWhenInsideWord_ThenEmbeddedMatchStillCountsAsBefore()
    {
        // Opt-in per possibility, so CheckboxMarkPossibilities is untouched.
        var request = Request("In");
        var result = new LabelGroupResult { Text = [LineOf("Point of abstraction:")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);
        Assert.Null(restricted.LabelGroupResult);
    }

    [Fact]
    public void WhenValueIsAStandaloneTickMarkAtTheEndOfTheLine_ThenExtractsIt()
    {
        // Checkbox glyphs are char.IsSymbol and were trimmed away as the line's last word. Runs
        // the real extraction path, so a regression in either place is caught.
        var request = RequestExceptWhenInsideWord("✓");
        var result = new LabelGroupResult { Text = [LineOf("Source of supply: ✓")] };

        var restricted = BaseMethod.RestrictToPossibility(request, result.Text);

        Assert.NotNull(restricted.LabelGroupResult);
        Assert.Equal("✓", restricted.LabelGroupResult.Text);
    }
}
