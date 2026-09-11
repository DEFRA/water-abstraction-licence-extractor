namespace WRADI.DocumentType.WrInspectionReport.Helpers;

public static class FormattingHelper
{
    public static string? GetTickedOrAcceptedStatus(string? rawRemainder)
    {
        return IsTick(rawRemainder)
            ? NormaliseSelectionMarks(rawRemainder)
            : null;
    }
    
    private static bool IsTick(string? content)
    {
        if (content == null)
        {
            return false;
        }
        
        var withoutSelectionMarks = content
            .Replace(":selected:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Replace(":unselected:", string.Empty, StringComparison.OrdinalIgnoreCase)
            .Trim();

        // A real tick/status answer ("✓", "N/A", "NI", "☑ ☐", etc. - see GetInOrderField's
        // Possibilities list) is always a handful of characters once Azure's own ":selected:"/
        // ":unselected:" annotation is stripped back out. Some templates put a full narrative
        // sentence in the same cell instead (e.g. "Source of supply: Lower Greensand at Warwick
        // Wold / Brewer St :selected:") - generous headroom above the longest real Possibility
        // ("☑ ☐", 3 chars) while still excluding any real sentence.
        const int maxTickAnswerLength = 8;
        
        return withoutSelectionMarks.Length <= maxTickAnswerLength;
    }

    private static string? NormaliseSelectionMarks(string? content)
    {
        if (content == null)
        {
            return content;
        }
        
        return content
            .Replace(":selected:", "✓", StringComparison.OrdinalIgnoreCase)
            .Replace(":unselected:", string.Empty, StringComparison.OrdinalIgnoreCase);
    }
}