using System.Text.RegularExpressions;

namespace WALE.Tools.Helpers;

/// <summary>
/// Pure, content-stream-level helpers for the WQ form reflow POC (see
/// WqFormPageSplitTests.TrySplitAndInsert): position tracking through a chain of relative
/// text-move operators, shifting/rewrapping a raw content fragment onto a new page, and
/// rebalancing marked-content/graphics-state nesting at a split point. Extracted out of the
/// test project so this math can be unit-tested directly rather than only indirectly via
/// full-PDF corpus regression.
/// </summary>
public static class PdfContentStreamHelper
{
    private static readonly Regex TmRegex = new(
        @"([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+([\d.\-]+)\s+Tm");

    /// <summary>A literal "(...)" run or a hex "&lt;...&gt;" run within one text-show operator.</summary>
    private static readonly Regex StringRunRegex = new(
        @"\((?<lit>(?:[^()\\]|\\.)*)\)|<(?<hex>[0-9A-Fa-f\s]*)>",
        RegexOptions.Singleline);

    /// <summary>
    /// A single relative line-position move: "TL" (sets leading for later "T*" moves), "TD"/"Td"
    /// (relative move, TD additionally sets leading to -dy), or "T*" (moves by 0,-leading). Text-
    /// show operators between moves don't affect position and are ignored entirely.
    /// </summary>
    public static readonly Regex MoveOperatorRegex = new(
        @"(?<tlval>[\d.\-]+)\s+TL\b" +
        @"|(?<tdx>[\d.\-]+)\s+(?<tdy>[\d.\-]+)\s+(?<tdop>TD|Td)\b" +
        @"|(?<tstar>T\*)");

    /// <summary>
    /// The absolute Tm components (matrix a/b/c/d and x) needed to build a synthetic Tm anchor
    /// for a mid-chain cut, deferred until the caller knows the final destination Y - the same
    /// information <see cref="TryComputeAbsolutePositionAtCutPoint"/> returns, just carried as
    /// data instead of baked into a string immediately (a cascade cut's final Y depends on where
    /// in the flow it lands, not a single fixed constant).
    /// </summary>
    public readonly record struct AnchorMatrix(string A, string B, string C, string D, double X);

    /// <summary>
    /// How a cascade fragment needs its font declared once shifted onto a different page than it
    /// was authored for.
    /// </summary>
    public enum FragmentWrapMode
    {
        /// <summary>SingleBlock tail/remainder: wrap as a fresh, self-contained "BT ... ET".</summary>
        WrapWithNewBt,

        /// <summary>MarkedContentPerFragment tail/remainder: already has its own leading "BT" from
        /// the cut point - insert the font declaration right after it instead of wrapping again.</summary>
        InsertFontAfterFirstBt,

        /// <summary>MarkedContentPerFragment head: a sequence of already-complete, self-contained
        /// fragments starting from the true page top - no font injection needed at all.</summary>
        NoFontNeeded,
    }

    /// <summary>
    /// Recovers the true absolute position of a "mid-chain" cut point: one reached from its
    /// nearest preceding absolute Tm not directly, but via one or more relative TD/Td/T* moves -
    /// e.g. a multi-line paragraph where the generator resets position once per paragraph and
    /// advances a line at a time from there, rather than resetting on every line. Walking the
    /// chain lets relocated content (which starts a brand new BT on a brand new page, with none
    /// of this TD-chain history to inherit) carry a synthetic Tm anchored at exactly the right
    /// spot.
    /// </summary>
    public static bool TryComputeAbsolutePositionAtCutPoint(
        string content, int cutIndex, out Match tmMatch, out double x, out double y)
    {
        x = 0;
        y = 0;

        var precedingTm = TmRegex.Matches(content[..cutIndex]).Cast<Match>().LastOrDefault();

        if (precedingTm == null)
        {
            tmMatch = null!;
            return false;
        }

        tmMatch = precedingTm;
        x = double.Parse(precedingTm.Groups[5].Value);
        y = double.Parse(precedingTm.Groups[6].Value);

        // TD/Td/T*'s own dx/dy/leading are expressed in TEXT space (the em-square Tm itself
        // scales), not device space - confirmed necessary on a real file (wq__302142): a
        // 7-line remainder under a "9.427 0 0 9.427 ... Tm" scale computed as descending only
        // ~12.8pt total (summing raw, unscaled TD dy's) when it actually spans ~121pt on the
        // page, an ~108pt error that went undetected until a whole-page-fold happened to land
        // right after it - text overlapping text isn't a structural defect pdftotext reports,
        // only a visual one a render catches. The Tm's own "a"/"d" components are exactly this
        // scale factor for the axis-aligned (no rotation/skew) matrices this document uses.
        var scaleX = double.Parse(precedingTm.Groups[1].Value);
        var scaleY = double.Parse(precedingTm.Groups[4].Value);

        var between = content[(precedingTm.Index + precedingTm.Length)..cutIndex];
        var leading = 0.0;

        foreach (Match move in MoveOperatorRegex.Matches(between))
        {
            if (move.Groups["tlval"].Success)
            {
                leading = double.Parse(move.Groups["tlval"].Value);
            }
            else if (move.Groups["tdop"].Success)
            {
                var dx = double.Parse(move.Groups["tdx"].Value);
                var dy = double.Parse(move.Groups["tdy"].Value);
                x += dx * scaleX;
                y += dy * scaleY;

                if (move.Groups["tdop"].Value == "TD")
                {
                    leading = -dy;
                }
            }
            else if (move.Groups["tstar"].Success)
            {
                y -= leading * scaleY;
            }
        }

        return true;
    }

    /// <summary>
    /// Shifts every Tm in a raw content fragment by a constant delta and prepares it to be
    /// self-contained wherever it ends up - used both for the cascade's pulled-forward head
    /// fragments (appended after another fragment's own closing ET on the same page) and its
    /// shifted remainders (becoming a page's entire new content). A non-null synthetic anchor
    /// bakes the caller-supplied final Y directly rather than relying on the generic per-Tm
    /// shift, since a mid-chain cut point has no Tm of its own to shift in the first place -
    /// only meaningful with <see cref="FragmentWrapMode.WrapWithNewBt"/>.
    /// </summary>
    public static string BuildShiftedFragment(
        string rawTail, double delta, double? syntheticFinalY, AnchorMatrix? syntheticAnchor,
        string fontDeclaration, FragmentWrapMode mode)
    {
        var shifted = TmRegex.Replace(rawTail, match =>
        {
            var y = double.Parse(match.Groups[6].Value) + delta;
            return $"{match.Groups[1].Value} {match.Groups[2].Value} {match.Groups[3].Value} " +
                   $"{match.Groups[4].Value} {match.Groups[5].Value} {y:0.####} Tm";
        });

        switch (mode)
        {
            case FragmentWrapMode.NoFontNeeded:
                return shifted;

            case FragmentWrapMode.InsertFontAfterFirstBt:
                var firstBt = shifted.IndexOf("BT", StringComparison.Ordinal);
                return firstBt < 0 ? shifted : shifted.Insert(firstBt + "BT".Length, $"\n{fontDeclaration}");

            default:
                if (syntheticAnchor != null && syntheticFinalY != null)
                {
                    var anchor = syntheticAnchor.Value;
                    var syntheticTm =
                        $"{anchor.A} {anchor.B} {anchor.C} {anchor.D} {anchor.X:0.####} {syntheticFinalY.Value:0.####} Tm";
                    return $"BT\n{fontDeclaration}\n{syntheticTm}\n{shifted}";
                }

                return $"BT\n{fontDeclaration}\n{shifted}";
        }
    }

    /// <summary>
    /// Closes whatever marked-content spans and graphics states are still open at a split point
    /// and reopens synthetic equivalents on the other side - shared by every reflow cut (the
    /// initial split and the cascade's own content/footer cuts) instead of each keeping its own
    /// copy.
    /// </summary>
    public static (string ClosedHead, string ReopenedTail) RebalanceMarkedContentAt(
        string headUpToSplit, string tailFromSplit)
    {
        var contentForDepthScan = StringRunRegex.Replace(headUpToSplit, "");
        var openSpanCount = 0;
        var openStateCount = 0;

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

        var closedHead = headUpToSplit
            + string.Concat(Enumerable.Repeat("\nEMC", openSpanCount))
            + string.Concat(Enumerable.Repeat("\nQ", openStateCount));
        var reopenedTail = string.Concat(Enumerable.Repeat("q\n", openStateCount))
            + string.Concat(Enumerable.Repeat("/Span <</MCID -1>> BDC\n", openSpanCount))
            + tailFromSplit;

        return (closedHead, reopenedTail);
    }

    /// <summary>
    /// Checks that a content fragment is self-contained: BT/ET, BDC/EMC, and q/Q each net to
    /// zero across the whole thing. Used as a final safety check on every fragment the reflow
    /// cascade is about to write - <see cref="RebalanceMarkedContentAt"/> is designed to always
    /// produce balanced halves, but a handful of real files have marked-content/graphics-state
    /// nesting its depth-counting doesn't model correctly, and an imbalanced fragment is a real,
    /// visible defect (a "Mismatched EMC operator" or "Restoring state" error) rather than
    /// something safe to ship and hope looks fine.
    /// </summary>
    public static bool IsMarkedContentBalanced(string fragment)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return true;
        }

        var stripped = StringRunRegex.Replace(fragment, "");
        var bdcBalance = 0;
        var stateBalance = 0;
        var textBalance = 0;

        foreach (Match tag in Regex.Matches(stripped, @"\bBDC\b|\bEMC\b|\bq\b|\bQ\b|\bBT\b|\bET\b"))
        {
            switch (tag.Value)
            {
                case "BDC": bdcBalance++; break;
                case "EMC": bdcBalance--; break;
                case "q": stateBalance++; break;
                case "Q": stateBalance--; break;
                case "BT": textBalance++; break;
                case "ET": textBalance--; break;
            }

            // Net-zero at the end isn't enough on its own - a fragment that closes before it
            // ever opens (e.g. "Q q") is still net-zero overall but fails the instant poppler
            // reaches that early close, since a self-contained fragment can never borrow an
            // open state from whatever came before it. Confirmed as the actual gap in a
            // net-zero-only version of this check on a real file (wq__202711): each fragment
            // balanced out by its own end, but one had already gone negative partway through -
            // "Restoring state when no valid states to pop" still appeared.
            if (bdcBalance < 0 || stateBalance < 0 || textBalance < 0)
            {
                return false;
            }
        }

        return bdcBalance == 0 && stateBalance == 0 && textBalance == 0;
    }
}
