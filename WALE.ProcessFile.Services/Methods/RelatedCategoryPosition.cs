using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class RelatedCategoryPosition
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var categoryItems = request.siblingMatches!
            .Where(match => match.MatchedLabel!.CategoryName == request.label.RelatedCategoryName)
            .OrderBy(match => match.LineNumber)
            .ToList();

        var ary = DataHelper.RemoveExcludesAndNotContains(request.label,
            [request.line!],
            false,
            true,
            out _,
            out _);
        
        var modifiedLine = ary.Count > 0 ? ary[0] : null;
        
        var modifiedPreviousLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.previousLines,
            false,
            true,
            out _,
            out _);
        
        var modifiedNextLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.nextLines,
            false,
            true,
            out _,
            out _);

        var matchedLabelLineNumbers = new List<int>();
        var relevantCategoryItems = new List<LabelGroupResult>();
        
        var lineStartsWithLabel = false;
        var valueBeforeLabel = false;
        
        foreach (var categoryItem in categoryItems)
        {
            if (categoryItem.MatchedLabel?.Name != request.label.RelatedName)
            {
                continue;
            }

            if (categoryItem.MatchedLabel?.Text != null)
            {
                foreach (var t in categoryItem.MatchedLabel.Text!)
                {
                    lineStartsWithLabel = modifiedLine?.Text?.StartsWith(t.Text, StringComparison.OrdinalIgnoreCase) == true;

                    if (lineStartsWithLabel)
                    {
                        break;
                    }
                }
            }

            matchedLabelLineNumbers.Add(categoryItem.LineNumber);
            relevantCategoryItems.Add(categoryItem);

            valueBeforeLabel =
                categoryItem.MatchedLabel?.Position is LabelPosition.LabelIsAfterTextToFind
                    or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter;
        }

        var matchedLabelLineNumber = matchedLabelLineNumbers
            .OrderBy(matchLineNumber => Math.Abs(request.line!.LineNumber - matchLineNumber))
            .FirstOrDefault();

        // If matching line starts with the label (and its lowercase), prefer the line before (e.g. 150 gallons\nper hour)
        if (modifiedLine?.LineNumber == matchedLabelLineNumber && lineStartsWithLabel && char.IsLower(modifiedLine.Text![0]))
        {
            matchedLabelLineNumber -= 1;
        }
        
        var matches = new List<DocumentLine>();
        List<DocumentLine> numberLines;
        
        foreach (var previousLine in modifiedPreviousLines.OrderByDescending(line => line.LineNumber))
        {
            foreach (var column in previousLine.Columns)
            {
                if (Number.AnyIsNumber([column.AsDocumentLine(previousLine)], request.label, out numberLines))
                {
                    matches.AddRange(numberLines);
                }                
            }
        }

        if (modifiedLine != null)
        {
            foreach (var column in modifiedLine.Columns)
            {
                if (Number.AnyIsNumber([column.AsDocumentLine(modifiedLine)], request.label, out numberLines))
                {
                    matches.AddRange(numberLines);
                }
            }
        }

        foreach (var nextLine in modifiedNextLines.OrderBy(line => line.LineNumber))
        {
            foreach (var column in nextLine.Columns)
            {
                if (Number.AnyIsNumber([column.AsDocumentLine(nextLine)], request.label, out numberLines))
                {
                    matches.AddRange(numberLines);
                }
            }
        }

        var allLines = new List<DocumentLine>();
        allLines.AddRange(modifiedPreviousLines);
        allLines.AddRange(modifiedNextLines);
        if (modifiedLine != null)allLines.Add(modifiedLine);
        
        var absoluteMatchesQuery = matches
            .OrderBy(match => Math.Abs(matchedLabelLineNumber - match.LineNumber))
            .ThenBy(match => match.LineNumber);
            
        var absoluteMatches = valueBeforeLabel
            ? absoluteMatchesQuery.ThenByDescending(match =>
                {
                    var line = allLines.First(x => x.LineNumber == match.LineNumber);

                    var lineText = line.Text.Replace(",", string.Empty);
                    var labelText = request.label.Text?.FirstOrDefault()?.Text ?? "[EMPTY_LABEL]";
                    
                    var matchIndexEnd = lineText.IndexOf(match.Text, StringComparison.Ordinal) + match.Text.Length;
                    var labelIndexStart = lineText.IndexOf(labelText, StringComparison.Ordinal);

                    var diff = matchIndexEnd - labelIndexStart;
                    if (diff > 0) diff = -diff - 100;
                    
                    return diff;
                }).ToList()
            : absoluteMatchesQuery.ThenBy(match =>
                {
                    var line = allLines.First(x => x.LineNumber == match.LineNumber);

                    var lineText = line.Text.Replace(",", string.Empty);
                    var labelText = request.label.Text?.FirstOrDefault()?.Text ?? "[EMPTY_LABEL]";
                
                    var matchIndexEnd = lineText.IndexOf(match.Text, StringComparison.Ordinal) + match.Text.Length;
                    var labelIndexStart = lineText.IndexOf(labelText, StringComparison.Ordinal);

                    var diff = matchIndexEnd - labelIndexStart;
                    return Math.Abs(diff);
                }).ToList();

        if ((request.label.Name == "PerDayValue" || request.label.Name == "PerYearValue")
            && request.line?.PageNumber >= 3
            && request.line.LineNumber >= 9)
        {
            
        }
        
        var returnList = new List<LabelGroupResult>();
        
        if (absoluteMatches.Count <= 0)
        {
            return Task.FromResult(returnList);
        }

        var categoryItemsOnLine = relevantCategoryItems
            .Where(x => x.LineNumber == matchedLabelLineNumber)
            .ToList();
        
        var howManyResults = request.label.FindMultipleOnSingleLine ?
            categoryItemsOnLine.Count
            : 1;

        if (howManyResults == 0)
        {
            howManyResults = 1; // TODO look into why this is - something to do with line numbers
            // being one out
        }
        
        var lines = absoluteMatches.Take(howManyResults);
        var lineCount = 0;
        
        foreach (var line in lines)
        {
            var labelGroupResult = request.labelGroupResult.Clone();
            
            var documentLine = new DocumentLine(
                PositionConstants.UnknownLineNumber,
                PositionConstants.UnknownPageNumber,
                line.Columns,
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate);

            labelGroupResult.Text = [documentLine];
            labelGroupResult.MatchedLabel = request.label;

            if (categoryItemsOnLine.Count >= lineCount + 2)
            {
                var categoryItem = categoryItemsOnLine[lineCount++];
                labelGroupResult.CharPosition = categoryItem.CharPosition;
            }
            else
            {
                // TODO - why?
            }

            // TODO should set match type
            FormattingHelper.RemoveRemoves(labelGroupResult, []); // TODO probably do something else

            returnList.AddRange(FilterIntoFormat(
                request,
                labelGroupResult,
                [line],
                false));
        }

        return ProcessSubLabelsAsync(request, returnList);
    }
}