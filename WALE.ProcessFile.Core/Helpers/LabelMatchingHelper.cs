using System.Text.RegularExpressions;
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
        return text?.Contains(subText, StringComparison.InvariantCultureIgnoreCase) == true;
    }
    
    public static bool PotentialMatchOnLabelLine(
        IEnumerable<TextAndLabel> textBeforeAndAfterLabel)
    {
        const string shortHyphen = "-";
        const string longHyphen = "—";
        
        foreach (var item in textBeforeAndAfterLabel)
        {
            var text = item.Text!.Trim();
            
            if (!FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(text)
                && text != shortHyphen
                && text != longHyphen)
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
        
        var combinedText = nextLineToContinueOnto != null ?
            $"{lineToCheck.Text} {nextLineToContinueOnto.Text}"
            : null;
        
        foreach (var labelTextOption in labelTextOptions!)
        {
            var labelText = labelTextOption.Text;

            if (labelTextOption.IsRegularExpression)
            {
                var matches = Regex.Matches(
                    lineToCheck.Text,
                    labelTextOption.Text,
                    RegexOptions.IgnoreCase);
                
                if (matches.Count > 0)
                {
                    matchedText = labelTextOption.Clone(labelTextOption.Text);

                    foreach (var match in matches.AsQueryable())
                    {
                        labelCharPosition = lineForPosition.Text.IndexOf(
                            match.Value,
                            StringComparison.InvariantCultureIgnoreCase);

                        if (labelCharPosition is -1 or 0)
                        {
                            return true;
                        }

                        var previousChar = lineForPosition.Text[labelCharPosition - 1];

                        if (previousChar is ' ' or ',' or '.')
                        {
                            return true;
                        }
                    }

                    return false;
                }
            }
            
            if (combinedText?.Contains(labelText, StringComparison.InvariantCultureIgnoreCase) == true)
            {
                var position2 = combinedText.IndexOf(labelText,
                    StringComparison.InvariantCultureIgnoreCase);
                var endPoint = position2 + labelText.Length;

                if (endPoint > lineToCheck.Text.Length && lineToCheck.Text.Length > position2)
                {
                    lineToCheck = lineToCheck.Clone();
                    lineToCheck.Columns.AddRange(nextLineToContinueOnto!.Columns);
                }
            }
            
            var lineMustContainEndOfLineMarker = labelText.Contains(PositionConstants.EndOfLineMarker);
            var labelTextWithoutMarkers = labelText;

            if (labelTextWithoutMarkers.Contains(PositionConstants.EndOfColumnMarker))
            {
                labelTextWithoutMarkers = labelTextWithoutMarkers
                    .Replace(PositionConstants.EndOfColumnMarker, string.Empty);
            }
            
            if (labelTextWithoutMarkers.Contains(PositionConstants.EndOfLineMarker))
            {
                labelTextWithoutMarkers = labelTextWithoutMarkers
                    .Replace(PositionConstants.EndOfLineMarker, string.Empty);                
            }
            
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
                    var labelTextWithSpaceBefore = $" {labelText}";
                    
                    var columnStartsWithLabelWithSpaceBefore =
                        column.Text.Contains(labelTextWithSpaceBefore, StringComparison.InvariantCultureIgnoreCase);
                    var columnEndsWithLabel =
                        column.Text.EndsWith(labelText, StringComparison.InvariantCultureIgnoreCase);
                    
                    var lineStartsWithLabelWithSpaceBefore =
                        lineToCheck.Text.Contains(labelTextWithSpaceBefore, StringComparison.InvariantCultureIgnoreCase);
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