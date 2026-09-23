using WALE.Tools.Helpers;
using Xunit;
using static WALE.Tools.Helpers.PdfContentStreamHelper;

namespace WALE.Tools.Tests;

/// <summary>
/// Focused unit tests for <see cref="PdfContentStreamHelper"/>'s pure content-stream math,
/// isolated from the full-PDF corpus regression in <see cref="WqFormPageSplitTests"/>. Each
/// case here is a minimal, hand-built content-stream fragment rather than a real sample file,
/// so a wrong result points straight at the formula instead of requiring a render to spot.
/// </summary>
public class PdfContentStreamHelperTests
{
    [Fact]
    public void WhenNoRelativeMovesFollowTheTm_ThenPositionIsTheTmsOwnXY()
    {
        var content = "BT\n1 0 0 1 82.916 726.36 Tm\n(Hello) Tj\nET";
        var cutIndex = content.Length;

        var found = TryComputeAbsolutePositionAtCutPoint(content, cutIndex, out _, out var x, out var y);

        Assert.True(found);
        Assert.Equal(82.916, x, precision: 3);
        Assert.Equal(726.36, y, precision: 3);
    }

    [Fact]
    public void WhenATdFollowsAnUnscaledTm_ThenItsDeltaAppliesAtFaceValue()
    {
        // Scale factors of 1 (an unscaled Tm) is the case a naive "just sum the raw TD deltas"
        // implementation gets right by coincidence - the regression case below is the one that
        // actually distinguishes scaled from unscaled handling.
        var content = "BT\n1 0 0 1 82.916 726.36 Tm\n0 -12 TD\n(Hello) Tj\nET";
        var cutIndex = content.IndexOf("(Hello)", StringComparison.Ordinal);

        var found = TryComputeAbsolutePositionAtCutPoint(content, cutIndex, out _, out _, out var y);

        Assert.True(found);
        Assert.Equal(714.36, y, precision: 3);
    }

    [Fact]
    public void WhenATdFollowsAScaledTm_ThenItsDeltaScalesByTheTmsOwnAxisFactor()
    {
        // The motivating regression: a real file (wq__302142) has a 7-line remainder under a
        // "9.427 0 0 9.427 ... Tm" scale. Treating TD's dy as already in device-space units (the
        // pre-fix bug) computed a ~12.8pt total descent across the 7 lines when the real spacing
        // was ~121pt - text-space TD deltas need multiplying by the Tm's own scale factor first.
        var content = "9.427 0 0 9.427 82.916 726.36 Tm\n0 -1.9158 TD\n(Row) Tj";
        var cutIndex = content.IndexOf("(Row)", StringComparison.Ordinal);

        var found = TryComputeAbsolutePositionAtCutPoint(content, cutIndex, out _, out _, out var y);

        Assert.True(found);
        // 726.36 + (-1.9158 * 9.427) ~= 708.3, not the naive 724.44 (726.36 - 1.9158).
        Assert.Equal(708.3, y, precision: 1);
    }

    [Fact]
    public void WhenTStarFollowsATlAndAScaledTm_ThenItMovesDownByLeadingTimesScale()
    {
        var content = "2 0 0 2 100 500 Tm\n10 TL\nT*\n(Line2) Tj";
        var cutIndex = content.IndexOf("(Line2)", StringComparison.Ordinal);

        var found = TryComputeAbsolutePositionAtCutPoint(content, cutIndex, out _, out _, out var y);

        Assert.True(found);
        Assert.Equal(480, y, precision: 3); // 500 - (10 * 2)
    }

    [Fact]
    public void WhenATdSetsANewLeading_ThenASubsequentTStarUsesThatLeadingNotTheOldOne()
    {
        var content = "1 0 0 1 0 100 Tm\n20 TL\n0 -5 TD\nT*\n(Line3) Tj";
        var cutIndex = content.IndexOf("(Line3)", StringComparison.Ordinal);

        var found = TryComputeAbsolutePositionAtCutPoint(content, cutIndex, out _, out _, out var y);

        Assert.True(found);
        // TD moves by -5 and resets leading to 5 (not 20); T* then moves by a further -5.
        Assert.Equal(90, y, precision: 3);
    }

    [Fact]
    public void WhenThereIsNoPrecedingTm_ThenItReturnsFalse()
    {
        var content = "(Hello) Tj";

        var found = TryComputeAbsolutePositionAtCutPoint(content, content.Length, out _, out _, out _);

        Assert.False(found);
    }

    [Theory]
    [InlineData("BT ET", true)]
    [InlineData("BT (a) Tj ET", true)]
    [InlineData("BT", false)]
    [InlineData("ET", false)]
    [InlineData("Q q", false)]
    [InlineData("q Q", true)]
    [InlineData("/Span <</MCID 1>> BDC (a) Tj EMC", true)]
    [InlineData("/Span <</MCID 1>> BDC (a) Tj", false)]
    [InlineData("", true)]
    public void IsMarkedContentBalanced_MatchesExpectedNesting(string fragment, bool expected)
    {
        Assert.Equal(expected, IsMarkedContentBalanced(fragment));
    }

    [Fact]
    public void IsMarkedContentBalanced_IgnoresOperatorLikeTextInsideLiteralStrings()
    {
        // "BT"/"EMC" etc. appearing inside a literal string's own text must not be mistaken for
        // real operators - only StringRunRegex-stripped content is scanned.
        var fragment = "BT (contains the word BDC and EMC as text) Tj ET";

        Assert.True(IsMarkedContentBalanced(fragment));
    }

    [Fact]
    public void RebalanceMarkedContentAt_ClosesOpenSpanAndStateInHead_ReopensInTail()
    {
        var head = "q\n/Span <</MCID 1>> BDC\n(before) Tj";
        var tail = "\n(after) Tj\nEMC\nQ";

        var (closedHead, reopenedTail) = RebalanceMarkedContentAt(head, tail);

        Assert.True(IsMarkedContentBalanced(closedHead));
        Assert.True(IsMarkedContentBalanced(reopenedTail));
        Assert.EndsWith("\nEMC\nQ", closedHead);
        Assert.StartsWith("q\n/Span <</MCID -1>> BDC\n", reopenedTail);
    }

    [Fact]
    public void RebalanceMarkedContentAt_WhenHeadIsAlreadyBalanced_LeavesBothSidesUnchanged()
    {
        var head = "BT (whole) Tj ET";
        var tail = "\nBT (next) Tj ET";

        var (closedHead, reopenedTail) = RebalanceMarkedContentAt(head, tail);

        Assert.Equal(head, closedHead);
        Assert.Equal(tail, reopenedTail);
    }

    [Fact]
    public void BuildShiftedFragment_WrapWithNewBt_ShiftsEveryTmByDeltaAndWrapsWithFont()
    {
        var raw = "1 0 0 1 10 100 Tm\n(a) Tj\n1 0 0 1 10 88 Tm\n(b) Tj";

        var result = BuildShiftedFragment(raw, delta: -50, null, null, "/F1 12 Tf", FragmentWrapMode.WrapWithNewBt);

        Assert.StartsWith("BT\n/F1 12 Tf\n", result);
        Assert.Contains("1 0 0 1 10 50 Tm", result);
        Assert.Contains("1 0 0 1 10 38 Tm", result);
    }

    [Fact]
    public void BuildShiftedFragment_WrapWithNewBt_WithSyntheticAnchor_BakesFinalYIntoASyntheticTm()
    {
        var raw = "(orphaned line, no Tm of its own) Tj";
        var anchor = new AnchorMatrix("1", "0", "0", "1", 42);

        var result = BuildShiftedFragment(raw, delta: 0, syntheticFinalY: 615.25, anchor, "/F1 10 Tf",
            FragmentWrapMode.WrapWithNewBt);

        Assert.Equal("BT\n/F1 10 Tf\n1 0 0 1 42 615.25 Tm\n(orphaned line, no Tm of its own) Tj", result);
    }

    [Fact]
    public void BuildShiftedFragment_InsertFontAfterFirstBt_InsertsRightAfterTheFirstBtOnly()
    {
        var raw = "BT\n1 0 0 1 0 0 Tm\n(a) Tj\nET\nBT\n1 0 0 1 0 -10 Tm\n(b) Tj\nET";

        var result = BuildShiftedFragment(raw, delta: 0, null, null, "/F1 12 Tf", FragmentWrapMode.InsertFontAfterFirstBt);

        Assert.StartsWith("BT\n/F1 12 Tf\n", result);
        // Only the first BT gets the font inserted after it - the second BT is untouched.
        Assert.Contains("ET\nBT\n1 0 0 1 0 -10 Tm", result);
    }

    [Fact]
    public void BuildShiftedFragment_NoFontNeeded_OnlyShiftsTmsAndAddsNoWrapping()
    {
        var raw = "BT\n1 0 0 1 0 200 Tm\n(a) Tj\nET";

        var result = BuildShiftedFragment(raw, delta: 25, null, null, "", FragmentWrapMode.NoFontNeeded);

        Assert.Equal("BT\n1 0 0 1 0 225 Tm\n(a) Tj\nET", result);
    }
}
