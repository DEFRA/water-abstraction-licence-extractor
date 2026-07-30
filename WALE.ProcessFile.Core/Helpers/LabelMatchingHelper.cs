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
    
    /// <summary>
    /// For this function to return true, a column or line must start with the text
    /// </summary>
    /// <param name="lineToCheck"></param>
    /// <param name="nextLineToContinueOnto"></param>
    /// <param name="lineForPosition"></param>
    /// <param name="labelTextOptions"></param>
    /// <param name="position"></param>
    /// <param name="lineIndex"></param>
    /// <param name="howManyLinesTotal"></param>
    /// <param name="matchedText"></param>
    /// <param name="labelStartPageNumber"></param>
    /// <param name="labelStartLineNumber"></param>
    /// <param name="labelStartCharIndex"></param>
    /// <param name="labelEndPageNumber"></param>
    /// <param name="labelEndLineNumber"></param>
    /// <param name="labelEndCharIndex"></param>
    /// <returns></returns>
    public static bool LineContainsLabel(
        DocumentLine lineToCheck,
        DocumentLine? nextLineToContinueOnto,
        DocumentLine lineForPosition,
        IReadOnlyList<TextToMatch>? labelTextOptions,
        LabelPosition position,
        int lineIndex,
        int howManyLinesTotal,
        out TextToMatch? matchedText,
        out int labelStartPageNumber,
        out int labelStartLineNumber,
        out int labelStartCharIndex,
        out int labelEndPageNumber,
        out int labelEndLineNumber,
        out int labelEndCharIndex)
    {
        matchedText = null;
        labelStartPageNumber = -1;
        labelStartLineNumber = -1;
        labelStartCharIndex = -1;
        labelEndPageNumber = -1;
        labelEndLineNumber = -1;
        labelEndCharIndex = -1;

        var labelHasNoTextToMatch = labelTextOptions == null;
        
        if (labelHasNoTextToMatch)
        {
            return true;
        }
        
        var combinedText = nextLineToContinueOnto != null ?
            $"{lineToCheck.Text} {nextLineToContinueOnto.Text}"
            : lineToCheck.Text;
        
        foreach (var labelTextOption in labelTextOptions!)
        {
            var labelText = labelTextOption.Text;

            if (labelTextOption.Regex != null)
            {
                var regexResult = LineContainsLabelRegex(
                    labelTextOption,
                    lineToCheck,
                    lineForPosition,
                    ref labelStartCharIndex,
                    ref matchedText);

                return regexResult.HasValue;
            }
            
            var labelTextWithoutMarkers = labelText
                .Replace(PositionConstants.EndOfColumnMarker, string.Empty)
                .Replace(PositionConstants.EndOfLineMarker, string.Empty);
            
            var combinedTextStartCharIndex = combinedText?.IndexOf(
                labelTextWithoutMarkers,
                StringComparison.OrdinalIgnoreCase);
            
            if (combinedTextStartCharIndex > -1)
            {
                var startsOnLine1 = combinedTextStartCharIndex < lineToCheck.Text.Length;
                
                labelStartCharIndex = startsOnLine1
                    ? combinedTextStartCharIndex.Value
                    : combinedTextStartCharIndex.Value - lineToCheck.Text.Length;
                labelStartLineNumber = startsOnLine1
                    ? lineToCheck.LineNumber
                    : nextLineToContinueOnto!.LineNumber;
                labelStartPageNumber = startsOnLine1
                    ? lineToCheck.PageNumber
                    : nextLineToContinueOnto!.PageNumber;
                
                var combinedTextEndCharIndex = combinedTextStartCharIndex + labelTextWithoutMarkers.Length;
                var endsOnLine2 = combinedTextEndCharIndex > lineToCheck.Text.Length;

                labelEndCharIndex = endsOnLine2
                    ? combinedTextEndCharIndex.Value - lineToCheck.Text.Length - 1
                    : combinedTextEndCharIndex.Value;
                labelEndLineNumber = endsOnLine2
                    ? nextLineToContinueOnto!.LineNumber
                    : lineToCheck.LineNumber;
                labelEndPageNumber = endsOnLine2
                    ? nextLineToContinueOnto!.PageNumber
                    : lineToCheck.PageNumber;
                
                if (startsOnLine1 && endsOnLine2)
                {
                    lineToCheck = lineToCheck.Clone();
                    lineToCheck.Columns.AddRange(nextLineToContinueOnto!.Columns);
                }
                
                matchedText = labelTextOption;
            }

            if (LookingForStartOfBlock(labelText))
            {
                var isFirstLine = lineIndex == 0;
                
                if (!isFirstLine)
                {
                    continue;
                }
                
                labelStartCharIndex = 0;
                labelStartLineNumber = lineToCheck.LineNumber;
                labelStartPageNumber = lineToCheck.PageNumber;
                labelEndCharIndex = 0;
                labelEndLineNumber = lineToCheck.LineNumber;
                labelEndPageNumber = lineToCheck.PageNumber;
                
                matchedText = labelTextOption;
                return true;
            }
            
            var mustContainEndOfColumnMarker = labelText.Contains(PositionConstants.EndOfColumnMarker);
            var mustContainEndOfLineMarker = labelText.Contains(PositionConstants.EndOfLineMarker);

            // No special conditions
            if (!string.IsNullOrEmpty(matchedText?.Text)
                && !mustContainEndOfColumnMarker
                && !mustContainEndOfLineMarker
                && labelTextOption is { ColumnMustStartWith: false, LineMustStartWith: false })
            {
                return true;
            }

            matchedText = null;
            
            var labelTextWithSpaceBefore = $" {labelText}";
            var labelTextWithAsteriskBefore = $"*{labelText}";            
            
            var lineStartsWithLabel =  lineToCheck.Text.StartsWith(labelTextWithoutMarkers, StringComparison.OrdinalIgnoreCase)
                || lineToCheck.Text.StartsWith(labelTextWithSpaceBefore, StringComparison.OrdinalIgnoreCase) // Might be redundant because of the way wr trim
                || lineToCheck.Text.StartsWith(labelTextWithAsteriskBefore, StringComparison.OrdinalIgnoreCase);
            
            var lineEndsWithLabel =
                lineToCheck.Text.EndsWith(labelTextWithoutMarkers, StringComparison.OrdinalIgnoreCase);
            
            foreach (var column in lineToCheck.Columns)
            {
                var columnStartsWithLabel = (bool?)null;

                if (mustContainEndOfColumnMarker)
                {
                    var columnEndsWithMarker = column.Text.EndsWith(
                        labelTextWithoutMarkers,
                        StringComparison.OrdinalIgnoreCase);
                    
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
                    if (lineEndsWithLabel)
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
                        || lineEndsWithLabel
                        || ColumnStartsWithLabel(column, labelTextWithoutMarkers, ref columnStartsWithLabel)
                        || column.Text.EndsWith(labelText, StringComparison.OrdinalIgnoreCase)
                        || column.Text.Contains(labelTextWithSpaceBefore, StringComparison.OrdinalIgnoreCase)
                        || column.Text.Contains(labelTextWithAsteriskBefore, StringComparison.OrdinalIgnoreCase))
                    {
                        matchedText = labelTextOption;
                        return true;
                    }
                }
            }
            
            var isLastLine = lineIndex == howManyLinesTotal - 1;
                
            if (position == LabelPosition.SplitAtLabel && isLastLine)
            {
                return true;
            }
        }
        
        return false;
    }
}