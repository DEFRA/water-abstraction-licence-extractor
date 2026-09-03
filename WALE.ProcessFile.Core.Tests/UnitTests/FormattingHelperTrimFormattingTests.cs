using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Core.Tests.UnitTests;

/// <summary>
/// FormattingHelper.TrimFormatting's punctuation trimming - specifically the fix for tick/
/// checkbox glyphs (✓☑☒☐) being silently stripped when they sit at the very start/end of the
/// trimmed text. They're char.IsSymbol, so without protecting them the same way '(' '&' ')'
/// ':' '/' already are, a standalone tick-mark answer disappears entirely - most visibly via
/// DocumentLineColumn.FilterWordsFromText, which calls this per-word with
/// trimPunctuationEnd=true for the last word, so a captured value like "Source of supply: ✓"
/// silently lost its own answer. Measured impact on the real WR51 corpus: the whole 13-field
/// LicenceProvisions grid's resolved rate was suppressed by this (masked until a separate fix
/// stopped an unrelated false-positive match from winning first).
/// </summary>
public class FormattingHelperTrimFormattingTests
{
    [Theory]
    [InlineData("✓")]
    [InlineData("☑")]
    [InlineData("☒")]
    [InlineData("☐")]
    public void WhenCheckboxGlyphIsTheWholeText_ThenSurvivesTrimmingBothEnds(string glyph)
    {
        var result = FormattingHelper.TrimFormatting(glyph, true, true);

        Assert.Equal(glyph, result);
    }

    [Theory]
    [InlineData("✓")]
    [InlineData("☑")]
    [InlineData("☒")]
    [InlineData("☐")]
    public void WhenCheckboxGlyphIsAtTheEndOfText_ThenSurvivesTrailingTrim(string glyph)
    {
        // The exact real regression: "Source of supply: ✓" is the last word of a captured
        // value ("✓" alone, once split on whitespace) - trimPunctuationEnd=true must not
        // strip it, or the answer is silently lost.
        var result = FormattingHelper.TrimFormatting($"Source of supply: {glyph}", false, true);

        Assert.EndsWith(glyph, result);
    }

    [Theory]
    [InlineData("✓")]
    [InlineData("☑")]
    [InlineData("☒")]
    [InlineData("☐")]
    public void WhenCheckboxGlyphIsAtTheStartOfText_ThenSurvivesLeadingTrim(string glyph)
    {
        var result = FormattingHelper.TrimFormatting($"{glyph} Yes", true, false);

        Assert.StartsWith(glyph, result);
    }

    [Fact]
    public void WhenOrdinarySymbolIsAtTheEnd_ThenStillGetsTrimmedAsBefore()
    {
        // Regression guard: only the specific checkbox glyphs are protected - this must not
        // become "stop trimming all symbols", which would silently change behaviour for every
        // other caller of TrimFormatting across both document types.
        var result = FormattingHelper.TrimFormatting("Some value*", false, true);

        Assert.Equal("Some value", result);
    }

    [Fact]
    public void WhenExistingProtectedCharactersAreAtTheEnd_ThenStillSurvive()
    {
        // The pre-existing protected set (')' ':' '&' '/') must be unaffected by adding the
        // checkbox glyphs alongside them.
        Assert.Equal("N/A", FormattingHelper.TrimFormatting("N/A", false, true));
        Assert.Equal("Value:", FormattingHelper.TrimFormatting("Value:", false, true));
        Assert.Equal("(Value)", FormattingHelper.TrimFormatting("(Value)", false, true));
    }

    [Fact]
    public void WhenWhitespaceSurroundsACheckboxGlyph_ThenBothAreHandledCorrectly()
    {
        var result = FormattingHelper.TrimFormatting("  ✓  ", true, true);

        Assert.Equal("✓", result);
    }
}
