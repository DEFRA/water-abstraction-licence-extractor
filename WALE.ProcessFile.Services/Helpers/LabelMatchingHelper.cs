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
        //return true;
        
        if (possibilities == null || possibilities.Count == 0)
        {
            return true;
        }
        
        return possibilities.Any(possibility =>
            text.Contains(possibility, StringComparison.InvariantCultureIgnoreCase));
    }
    
    public static bool LineContainsLabel(
        DocumentLine line,
        IReadOnlyList<string>? labelTextOptions,
        LabelPosition position,
        int lineCount,
        int howManyLinesTotal,
        out string? matchedText)
    {
        matchedText = null;

        var labelHasNoTextToMatch = labelTextOptions == null;
        
        if (labelHasNoTextToMatch)
        {
            return true;
        }
        
        foreach (var labelText in labelTextOptions!)
        {
            var firstLine = lineCount == 0;
            var isStartOfBlock = labelText.Equals(PositionConstants.StartOfBlockMarker,
                StringComparison.InvariantCultureIgnoreCase);
            
            if (firstLine && isStartOfBlock)
            {
                matchedText = labelText;
                return true;
            }
         
            var isEndOfLineMarker = labelText.Contains(PositionConstants.EndOfLineMarker);

            if (isEndOfLineMarker)
            {
                var labelTextWithoutMarker =
                    labelText.Replace(PositionConstants.EndOfLineMarker, string.Empty);

                var lineEndsWithMarker =
                    line.Text.EndsWith(labelTextWithoutMarker, StringComparison.InvariantCultureIgnoreCase);
                
                if (lineEndsWithMarker)
                {
                    matchedText = labelTextWithoutMarker;
                    return true;
                }
            }
            else
            {
                var lineStartsWithLabel =
                    line.Text.StartsWith(labelText, StringComparison.InvariantCultureIgnoreCase);
                var lineStartsWithLabelWithSpaceBefore =
                    line.Text.Contains($" {labelText}", StringComparison.InvariantCultureIgnoreCase);
                
                if (lineStartsWithLabel || lineStartsWithLabelWithSpaceBefore)
                {
                    matchedText = labelText;
                    return true;
                }
            }

            var isLastLine = lineCount == howManyLinesTotal - 1;
            
            if (position == LabelPosition.Split && isLastLine)
            {
                return true;
            }
        }

        return false;
    }
}