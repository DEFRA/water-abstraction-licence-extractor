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
        const string shortHyphen = "-";
        const string longHyphen = "—";
        
        foreach (var (text, _) in textBeforeAndAfterLabel)
        {
            if (!FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(text)
                && text!.Trim() != shortHyphen
                && text.Trim() != longHyphen)
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

        const string startOfBlockMarker = "[START_OF_BLOCK]";
        const string endOfLineMarker = "[END_OF_LINE]";
        
        foreach (var textItem in labelText)
        {
            if (lineCount == 0
                && textItem.Equals(startOfBlockMarker, StringComparison.InvariantCultureIgnoreCase))
            {
                matchedText = textItem;
                return true;
            }
         
            var mustEndLine = textItem.Contains(endOfLineMarker);

            if (mustEndLine)
            {
                var tItem = textItem.Replace(endOfLineMarker, string.Empty);

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