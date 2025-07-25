using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public class LabelMatchingHelper
{
    public static bool ContainsForbiddenText(DocumentLine? line, LabelToMatch label)
    {
        return label.MustNotContain?
            .Any(mustNotContainText =>
                line?.Text.Contains(mustNotContainText, StringComparison.InvariantCultureIgnoreCase) == true) == true;
    }
    
    public static bool PotentialMatchOnLabelLine(
        IEnumerable<(string? Text, LabelToMatch Label)> textBeforeAndAfterLabel)
    {
        foreach (var (text, _) in textBeforeAndAfterLabel)
        {
            if (!FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(text)
                && text!.Trim() != "-"
                && text.Trim() != "—")
            {
                return true;
            }
        }

        return false;
    }
    
    public static bool LineContainsLabel(
        DocumentLine line,
        IReadOnlyList<string>? labelText,
        LabelPosition position,
        int lineCount,
        int howManyLinesTotal,
        out string? matchedText)
    {
        if (labelText == null)
        {
            matchedText = null;
            return true;
        }
        
        foreach (var textItem in labelText)
        {
            if (lineCount == 0
                && textItem.Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase))
            {
                matchedText = textItem;
                return true;
            }
         
            var mustEndLine = textItem.Contains("[END_OF_LINE]");

            if (mustEndLine)
            {
                var tItem = textItem.Replace("[END_OF_LINE]", string.Empty);

                if (line.Text.EndsWith(tItem, StringComparison.InvariantCultureIgnoreCase))
                {
                    matchedText = tItem;
                    return true;                    
                }
            }
            else
            {
                if (line.Text.StartsWith(textItem, StringComparison.InvariantCultureIgnoreCase)
                    || line.Text.Contains($" {textItem}", StringComparison.InvariantCultureIgnoreCase))
                {
                    matchedText = textItem;
                    return true;
                }
            }

            if (position == LabelPosition.Split && lineCount == howManyLinesTotal - 1)
            {
                matchedText = null;
                return true;
            }
        }

        matchedText = null;
        return false;
    }
}