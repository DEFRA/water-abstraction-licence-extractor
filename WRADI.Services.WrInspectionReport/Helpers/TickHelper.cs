namespace WRADI.DocumentType.WrInspectionReport.Helpers;

public static class TickHelper
{
    // A real tick/status answer ("✓", "N/A", "NI", "☑ ☐", etc. - see GetInOrderField's
    // Possibilities list) is always a handful of characters once Azure's own ":selected:"/
    // ":unselected:" annotation is stripped back out. Some templates put a full narrative
    // sentence in the same cell instead (e.g. "Source of supply: Lower Greensand at Warwick
    // Wold / Brewer St :selected:") - generous headroom above the longest real Possibility
    // ("☑ ☐", 3 chars) while still excluding any real sentence.
    private const int MaxTickAnswerLength = 8;

    public static string? GetTickedOrAcceptedStatus(string? rawRemainder)
    {
        if (rawRemainder == null)
        {
            return null;
        }

        var withoutSelectionMarks = rawRemainder
            .Replace(":selected:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(":unselected:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        if (withoutSelectionMarks.Length <= MaxTickAnswerLength)
        {
            return NormaliseSelectionMarks(rawRemainder);
        }

        // T4/T6 (and some T1) templates draw the tick and a narrative elaboration in the SAME
        // cell, tick first (e.g. "✓ River Beult & River Medway in the parish of Yalding Kent") -
        // PdfClownGridTableExtractorService reads these merged, unlike Azure DI's own cell shape
        // this method was originally tuned against (where the equivalent narrative case trails a
        // ":selected:" annotation at the END, not the start - still correctly excluded below).
        // Deliberately narrow to a short, letter-free leading token (covers every ordinary tick
        // glyph plus the Wingdings-style Private Use Area codepoints real WR51 exports also use -
        // see WrInspectionReportTextBasedLabelConfiguration's InOrderPossibilities - without hardcoding a
        // third copy of that glyph list to keep in sync), not the letter-based Y/N/X/In/Not/NI/
        // N-A possibilities: those collide both with ordinary short English/numeric answers a
        // genuinely narrative-only cell can start with, AND - the sharper risk, found via the
        // golden-set harness - with those same words appearing naturally *later* in the
        // narrative itself (e.g. "...in the parish of Yalding Kent" contains the standalone word
        // "in", which would otherwise satisfy the "In" possibility before TableMatcherHelper ever
        // reaches the real "✓"). Returning only the leading token - never the full remainder -
        // keeps that downstream Contains-based possibility scan from ever seeing the narrative
        // tail at all, which is what actually closes that hole.
        var leadingToken = withoutSelectionMarks.Split(' ', 2)[0];

        var isUnambiguousTickGlyph =
            leadingToken.Length is 1 or 2 && leadingToken.All(c => !char.IsLetterOrDigit(c));

        return isUnambiguousTickGlyph ? NormaliseSelectionMarks(leadingToken) : null;
    }

    private static string NormaliseSelectionMarks(string content)
    {
        return content
            .Replace(":selected:", "✓", StringComparison.OrdinalIgnoreCase)
            .Replace(":unselected:", string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}
