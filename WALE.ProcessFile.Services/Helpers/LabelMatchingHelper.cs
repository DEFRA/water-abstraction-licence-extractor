using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class LabelMatchingHelper
{
    public static bool ContainsForbiddenText(DocumentLine? line, LabelToMatch label)
    {
        return label.MustNotContain?
            .Any(mustNotContainText =>
                LineContainsForbiddenText(line, mustNotContainText)) == true;
    }

    private static bool LineContainsForbiddenText(DocumentLine? line, string mustNotContainText)
    {
        return line?.Text.Contains(mustNotContainText, StringComparison.InvariantCultureIgnoreCase) == true;
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

    private static bool PossibilityIsNullOrContainsPossibility(
        string text,
        IReadOnlyList<string>? possibilities)
    {
        return true;
        
        if (possibilities == null || possibilities.Count == 0)
        {
            return true;
        }
        
        var matchedPossibility = possibilities.Any(possibility =>
            text.Contains(possibility, StringComparison.InvariantCultureIgnoreCase));

        return matchedPossibility;
    }
    
    public static bool LineContainsLabel(
        DocumentLine line,
        IReadOnlyList<string>? labelText,
        IReadOnlyList<string>? possibilities,
        LabelPosition position,
        int lineCount,
        int howManyLinesTotal,
        out string? matchedText)
    {
        matchedText = null;
        
        if (labelText == null)
        {
            return PossibilityIsNullOrContainsPossibility(line.Text, possibilities);
        }
        
        foreach (var textItem in labelText)
        {
            if (lineCount == 0
                && textItem.Equals(PositionConstants.StartOfBlockMarker, StringComparison.InvariantCultureIgnoreCase))
            {
                matchedText = textItem;
                return PossibilityIsNullOrContainsPossibility(textItem, possibilities);
            }
         
            var mustEndLine = textItem.Contains(PositionConstants.EndOfLineMarker);

            if (mustEndLine)
            {
                var tItem = textItem.Replace(PositionConstants.EndOfLineMarker, string.Empty);

                if (line.Text.EndsWith(tItem, StringComparison.InvariantCultureIgnoreCase))
                {
                    matchedText = tItem;
                    return PossibilityIsNullOrContainsPossibility(tItem, possibilities);
                }
            }
            else
            {
                if (line.Text.StartsWith(textItem, StringComparison.InvariantCultureIgnoreCase)
                    || line.Text.Contains($" {textItem}", StringComparison.InvariantCultureIgnoreCase))
                {
                    matchedText = textItem;
                    return PossibilityIsNullOrContainsPossibility(textItem, possibilities);
                }
            }

            if (position != LabelPosition.Split || lineCount != howManyLinesTotal - 1)
            {
                continue;
            }
            
            return true;
        }

        return false;
    }
}