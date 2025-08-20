using WALE.ProcessFile.Services.Constants;
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
        
        var labelGroupResult = request.labelGroupResult;//.Clone();
                    
        var categoryItems = request.siblingMatches!
            .Where(match => match.MatchedLabel!.CategoryName == request.label.RelatedCategoryName)
            .OrderBy(match => match.LineNumber)
            .ToList();

        var ary = DataHelper.RemoveExcludesAndNotContains(request.label,
            [request.line!],
            false,
            out _);
        
        var modifiedLine = ary.Count > 0 ? ary[0] : null;
        
        var modifiedPreviousLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.previousLines,
            false,
            out _);
        
        var modifiedNextLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.nextLines,
            false,
            out _);        
        
        var matchedLabelLineNumber = PositionConstants.UnknownLineNumber;
        var lineStartsWithLabel = false;
        
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

            matchedLabelLineNumber = categoryItem.LineNumber;

            break;
        }

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

        var absoluteMatches = matches
            .OrderBy(match => Math.Abs(matchedLabelLineNumber - match.LineNumber))
            .ThenBy(match => match.LineNumber)
            .ToList();

        var returnList = new List<LabelGroupResult>();
        
        if (absoluteMatches.Count <= 0)
        {
            return Task.FromResult(returnList);
        }

        var line = absoluteMatches.First();

        var documentLine = new DocumentLine(
            PositionConstants.UnknownLineNumber,
            PositionConstants.UnknownPageNumber,
            line.Columns,
            PositionConstants.UnknownCoordinate,
            PositionConstants.UnknownCoordinate,
            PositionConstants.UnknownCoordinate);

        labelGroupResult.Text = [documentLine];
        labelGroupResult.MatchedLabel = request.label;

        // TODO should set match type
        FormattingHelper.RemoveRemoves(labelGroupResult, []); // TODO probably do something else

        returnList.Add(labelGroupResult);
        returnList = FilterIntoFormat(
            request,
            labelGroupResult,
            absoluteMatches.Take(1).ToList(),
            false);

        return ProcessSubLabelsAsync(request, returnList);
    }
}