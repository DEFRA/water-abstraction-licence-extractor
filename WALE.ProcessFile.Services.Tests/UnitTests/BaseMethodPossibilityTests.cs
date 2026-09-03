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
/// blank field as a match - it silently vanished instead of surviving as Blank. See
/// analysis/07-label-matching-and-debugging.md for how this was found on the real WR51 corpus
/// (77% of OtherProvisions, 53% of SpecialConditions reporting DidntMatch instead of Blank).
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
}
