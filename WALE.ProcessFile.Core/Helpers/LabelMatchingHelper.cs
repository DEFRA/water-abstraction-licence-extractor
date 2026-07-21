using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Core.Helpers;

public static class LabelMatchingHelper
{
    public static bool ShouldSkipLineAsForbidden(string? text, LabelToMatch label)
    {
        return label.SkipLineWhenContains?
            .Any(mustNotContainText =>
                TextContainsText(text, mustNotContainText)) == true;
    }
    
    public static bool ShouldSkipResultAsForbidden(string? text, LabelToMatch label)
    {
        return label.IgnoreMatchIfContains?
            .Any(mustNotContainText =>
                TextContainsText(text, mustNotContainText)) == true;
    }
    
    public static bool ShouldSkipBlockAsForbidden(string? text, LabelToMatch label)
    {
        return label.IgnoreBlockIfContains?
            .Any(mustNotContainText =>
                TextContainsText(text, mustNotContainText)) == true;
    }

    private static bool TextContainsText(string? text, string subText)
    {
        return text?.Contains(subText, StringComparison.OrdinalIgnoreCase) == true;
    }
    
    public static bool PotentialMatchOnLabelLine(
        IEnumerable<TextAndLabel> textBeforeAndAfterLabel)
    {
        const string shortHyphen = "-";
        const string longHyphen = "—";
        
        foreach (var item in textBeforeAndAfterLabel)
        {
            foreach (var columnText in item.ColumnsText!)
            {
                var text = columnText.Trim();
            
                if (!FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(text)
                    && text != shortHyphen
                    && text != longHyphen)
                {
                    return true;
                }   
            }
        }

        return false;
    }

    private static bool? LineContainsLabelRegex(
        TextToMatch labelTextOption,
        DocumentLine lineToCheck,
        DocumentLine lineForPosition,
        ref int labelCharPosition,
        ref TextToMatch? matchedText)
    {
        var matches = labelTextOption.Regex!.Matches(lineToCheck.Text);

        if (matches.Count <= 0)
        {
            return null;
        }
        
        matchedText = labelTextOption.Clone(labelTextOption.Text);

        foreach (var match in matches.AsQueryable())
        {
            labelCharPosition = lineForPosition.Text.IndexOf(
                match.Value,
                StringComparison.OrdinalIgnoreCase);

            if (labelCharPosition is -1 or 0)
            {
                return true;
            }

            var previousChar = lineForPosition.Text[labelCharPosition - 1];
            var firstChar = match.Value[0];
                        
            if (previousChar is ' ' or ',' or '.'
                || firstChar is ' ' or ',' or '.')
            {
                return true;
            }
        }

        return false;
    }

    private static bool LookingForStartOfBlock(string labelText)
    {
        return labelText.Equals(PositionConstants.StartOfBlockMarker);
    }

    private static bool ColumnStartsWithLabel(DocumentLineColumn column, string labelTextWithoutMarkers, ref bool? resultOut)
    {
        if (resultOut.HasValue)
        {
            return resultOut.Value;
        }
        
        var result = column.Text.StartsWith(labelTextWithoutMarkers, StringComparison.OrdinalIgnoreCase);

        resultOut = result;
        return result;
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
        
        var combinedText = nextLineToContinueOnto != null ?
            $"{lineToCheck.Text} {nextLineToContinueOnto.Text}"
            : null;
        
        foreach (var labelTextOption in labelTextOptions!)
        {
            var labelText = labelTextOption.Text;

            if (labelTextOption.Regex != null)
            {
                var regexResult = LineContainsLabelRegex(
                    labelTextOption,
                    lineToCheck,
                    lineForPosition,
                    ref labelCharPosition,
                    ref matchedText);

                return regexResult.HasValue;
            }
            
            var combinedTextPosition = combinedText?.IndexOf(labelText,
                StringComparison.InvariantCultureIgnoreCase);
            
            if (combinedTextPosition > -1)
            {
                var endPoint = combinedTextPosition + labelText.Length;

                if (endPoint > lineToCheck.Text.Length && lineToCheck.Text.Length > combinedTextPosition)
                {
                    lineToCheck = lineToCheck.Clone();
                    lineToCheck.Columns.AddRange(nextLineToContinueOnto!.Columns);
                }
            }
            
            var isFirstLine = lineCount == 0;

            if (isFirstLine && LookingForStartOfBlock(labelText))
            {
                matchedText = labelTextOption;
                labelCharPosition = 0;
                
                return true;
            }
            
            var mustContainEndOfColumnMarker = labelText.Contains(PositionConstants.EndOfColumnMarker);
            var mustContainEndOfLineMarker = labelText.Contains(PositionConstants.EndOfLineMarker);

            var labelTextWithoutMarkers = labelText
                .Replace(PositionConstants.EndOfColumnMarker, string.Empty)
                .Replace(PositionConstants.EndOfLineMarker, string.Empty);
            
            var lineStartsWithLabel =
                lineToCheck.Text.StartsWith(labelTextWithoutMarkers, StringComparison.InvariantCultureIgnoreCase);
            
            labelCharPosition = lineForPosition.Text.IndexOf(
                labelTextWithoutMarkers,
                StringComparison.InvariantCultureIgnoreCase);
            
            var labelTextWithSpaceBefore = $" {labelText}";
            
            var lineStartsWithLabelWithSpaceBefore =
                lineToCheck.Text.Contains(labelTextWithSpaceBefore, StringComparison.InvariantCultureIgnoreCase);
            var lineEndsWithLabel =
                lineToCheck.Text.EndsWith(labelText, StringComparison.InvariantCultureIgnoreCase);
            var lineEndsWithMarker =
                lineToCheck.Text.EndsWith(labelTextWithoutMarkers, StringComparison.InvariantCultureIgnoreCase);
            
            foreach (var column in lineToCheck.Columns)
            {
                var columnStartsWithLabel = (bool?)null;

                if (mustContainEndOfColumnMarker)
                {
                    var columnEndsWithMarker =
                        column.Text.EndsWith(labelTextWithoutMarkers, StringComparison.InvariantCultureIgnoreCase);
                    
                    if (columnEndsWithMarker)
                    {
                        if (labelTextOption.ColumnMustStartWith)
                        {
                            if (ColumnStartsWithLabel(column, labelTextWithoutMarkers, ref columnStartsWithLabel))
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
                else if (mustContainEndOfLineMarker)
                {
                    if (lineEndsWithMarker)
                    {
                        if (labelTextOption.ColumnMustStartWith)
                        {
                            if (ColumnStartsWithLabel(column, labelTextWithoutMarkers, ref columnStartsWithLabel))
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
                    if (labelTextOption.ColumnMustStartWith)
                    {
                        if (ColumnStartsWithLabel(column, labelTextWithoutMarkers, ref columnStartsWithLabel))
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
                    else if (lineStartsWithLabel
                        || lineStartsWithLabelWithSpaceBefore
                        || lineEndsWithLabel
                        || ColumnStartsWithLabel(column, labelTextWithoutMarkers, ref columnStartsWithLabel)
                        || column.Text.EndsWith(labelText, StringComparison.InvariantCultureIgnoreCase)
                        || column.Text.Contains(labelTextWithSpaceBefore, StringComparison.InvariantCultureIgnoreCase))
                    {
                        matchedText = labelTextOption;
                        return true;
                    }
                }
            }
            
            var isLastLine = lineCount == howManyLinesTotal - 1;
                
            if (position == LabelPosition.SplitAtLabel && isLastLine)
            {
                return true;
            }
        }

        return false;
    }
}