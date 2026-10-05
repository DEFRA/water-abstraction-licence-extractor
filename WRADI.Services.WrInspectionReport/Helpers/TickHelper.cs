namespace WRADI.DocumentType.WrInspectionReport.Helpers;

public static class TickHelper
{
    // A real tick/status answer is a handful of characters once Azure's ":selected:" annotation
    // is stripped. Some templates put a full narrative sentence in the same cell instead, so this
    // leaves headroom over the longest real Possibility ("☑ ☐", 3 chars) but excludes sentences.
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

        // T4/T6 and some T1 templates draw the tick and a narrative elaboration in the same cell,
        // tick first ("✓ River Beult & River Medway in the parish of Yalding Kent"), which PdfClown
        // reads merged - unlike the Azure DI cell shape this was tuned against, where the narrative
        // trails a ":selected:" at the end instead.
        //
        // Matches only a short letter-free leading token, which covers ordinary tick glyphs and the
        // Wingdings PUA codepoints WR51 exports use without keeping a third copy of that glyph
        // list in sync. Deliberately not the letter-based Y/N/X/In/Not possibilities: those collide
        // with short answers a narrative-only cell can start with, and - the sharper risk, found
        // via the golden set - with the same words appearing later in the narrative ("the parish of
        // Yalding" contains "in", satisfying the "In" possibility before the real "✓" is reached).
        // Returning only the leading token stops the downstream Contains scan ever seeing the
        // narrative tail, which is what closes that hole.
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
