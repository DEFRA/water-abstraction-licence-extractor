using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Helpers;

public static class LabelMatchingHelper
{
    public static bool ShouldSkipLineAsForbidden(string? text, LabelToMatch label)
    {
        return label.SkipLineWhenContains?
            .Any(mustNotContainText =>
                TextContainsForbiddenText(text, mustNotContainText)) == true;
    }
    
    public static bool ShouldSkipResultAsForbidden(string? text, LabelToMatch label)
    {
        return label.IgnoreMatchIfContains?
            .Any(mustNotContainText =>
                TextContainsForbiddenText(text, mustNotContainText)) == true;
    }

    private static bool TextContainsForbiddenText(string? text, string mustNotContainText)
    {
        return text?.Contains(mustNotContainText, StringComparison.InvariantCultureIgnoreCase) == true;
    }
    
    public static bool PotentialMatchOnLabelLine(
        IEnumerable<TextAndLabel> textBeforeAndAfterLabel)
    {
        const string shortHyphen = "-";
        const string longHyphen = "—";
        
        foreach (var item in textBeforeAndAfterLabel)
        {
            if (!FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(item.Text)
                && item.Text!.Trim() != shortHyphen
                && item.Text.Trim() != longHyphen)
            {
                return true;
            }
        }

        return false;
    }
    
    public static bool LineContainsLabel(
        DocumentLine lineToCheck,
        DocumentLine? nextLineToContinueOnto,
        DocumentLine lineForPosition,
        IReadOnlyList<TextToMatch>? labelTextOptions,
        LabelPosition position,
        int lineCount,
        int howManyLinesTotal,
        out TextToMatch? matchedText,
        out int labelCharPosition)
    {
        matchedText = null;
        labelCharPosition = -1;

        var labelHasNoTextToMatch = labelTextOptions == null;
        
        if (labelHasNoTextToMatch)
        {
            return true;
        }
        
        foreach (var labelTextOption in labelTextOptions!)
        {
            var labelText = labelTextOption.Text;
            var lineMustContainEndOfLineMarker = labelText.Contains(PositionConstants.EndOfLineMarker);
            
            var labelTextWithoutMarkers = labelText
                .Replace(PositionConstants.EndOfColumnMarker, string.Empty)
                .Replace(PositionConstants.EndOfLineMarker, string.Empty);
            
            var firstLine = lineCount == 0;
            var isStartOfBlock = labelText.Equals(PositionConstants.StartOfBlockMarker,
                StringComparison.InvariantCultureIgnoreCase);
        
            if (firstLine && isStartOfBlock)
            {
                matchedText = labelTextOption;
                labelCharPosition = 0;
                
                return true;
            }
            
            var lineStartsWithLabel =
                lineToCheck.Text.StartsWith(labelTextWithoutMarkers, StringComparison.InvariantCultureIgnoreCase);
            
            labelCharPosition = lineForPosition.Text.IndexOf(
                labelTextWithoutMarkers,
                StringComparison.InvariantCultureIgnoreCase);
            
            foreach (var column in lineToCheck.Columns)
            {
                var columnStartsWithLabel =
                    column.Text.StartsWith(labelTextWithoutMarkers, StringComparison.InvariantCultureIgnoreCase);

                var columnMustContainEndOfColumnMarker = labelText.Contains(PositionConstants.EndOfColumnMarker);
                
                if (columnMustContainEndOfColumnMarker)
                {
                    var columnEndsWithMarker =
                        column.Text.EndsWith(labelTextWithoutMarkers, StringComparison.InvariantCultureIgnoreCase);
                    
                    if (columnEndsWithMarker)
                    {
                        if (labelTextOption.ColumnMustStartWith)
                        {
                            if (columnStartsWithLabel)
                            {
                                matchedText = labelTextOption.Clone(labelTextWithoutMarkers);
                                return true;
                            }
                        }
                        else if (labelTextOption.LineMustStartWith)
                        {
                            if (lineStartsWithLabel)
                            {
                                matchedText = labelTextOption.Clone(labelTextWithoutMarkers);
                                return true;
                            }
                        }
                        else
                        {
                            matchedText = labelTextOption.Clone(labelTextWithoutMarkers);
                            return true;                        
                        }
                    }
                }
                else if (lineMustContainEndOfLineMarker)
                {
                    var lineEndsWithMarker =
                        lineToCheck.Text.EndsWith(labelTextWithoutMarkers, StringComparison.InvariantCultureIgnoreCase);                    
                    
                    if (lineEndsWithMarker)
                    {
                        if (labelTextOption.ColumnMustStartWith)
                        {
                            if (columnStartsWithLabel)
                            {
                                matchedText = labelTextOption.Clone(labelTextWithoutMarkers);
                                return true;
                            }
                        }
                        else if (labelTextOption.LineMustStartWith)
                        {
                            if (lineStartsWithLabel)
                            {
                                matchedText = labelTextOption.Clone(labelTextWithoutMarkers);
                                return true;
                            }
                        }
                        else
                        {
                            matchedText = labelTextOption.Clone(labelTextWithoutMarkers);
                            return true;                        
                        }
                    }
                }
                else
                {
                    var columnStartsWithLabelWithSpaceBefore =
                        column.Text.Contains($" {labelText}", StringComparison.InvariantCultureIgnoreCase);
                    var columnEndsWithLabel =
                        column.Text.EndsWith(labelText, StringComparison.InvariantCultureIgnoreCase);
                    
                    var lineStartsWithLabelWithSpaceBefore =
                        lineToCheck.Text.Contains($" {labelText}", StringComparison.InvariantCultureIgnoreCase);
                    var lineEndsWithLabel =
                        lineToCheck.Text.EndsWith(labelText, StringComparison.InvariantCultureIgnoreCase);

                    if (labelTextOption.ColumnMustStartWith)
                    {
                        if (columnStartsWithLabel)
                        {
                            matchedText = labelTextOption;
                            return true;
                        }
                    }
                    else if (labelTextOption.LineMustStartWith)
                    {
                        if (lineStartsWithLabel)
                        {
                            matchedText = labelTextOption;
                            return true;
                        }
                    }
                    else if (columnStartsWithLabel
                        || lineStartsWithLabel
                        || columnStartsWithLabelWithSpaceBefore
                        || lineStartsWithLabelWithSpaceBefore
                        || columnEndsWithLabel
                        || lineEndsWithLabel)
                    {
                        matchedText = labelTextOption;
                        return true;
                    }
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