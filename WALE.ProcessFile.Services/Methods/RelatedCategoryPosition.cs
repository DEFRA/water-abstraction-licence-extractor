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
        
        var labelGroupResult = request.labelGroupResult.Clone();
                    
        var categoryItems = request.siblingMatches!
            .Where(match => match.MatchedLabel!.CategoryName == request.label.RelatedCategoryName)
            .OrderBy(match => match.LineNumber)
            .ToList();

        var modifiedPreviousLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.previousLines,
            out _);
        
        var modifiedNextLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.nextLines,
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
                    lineStartsWithLabel = request.line!.Text.StartsWith(t, StringComparison.OrdinalIgnoreCase);

                    if (lineStartsWithLabel)
                    {
                        break;
                    }
                }
            }

            matchedLabelLineNumber = categoryItem.LineNumber;

            break;
        }

        // If matching line starts with the label, prefer the line before
        if (request.line!.LineNumber == matchedLabelLineNumber && lineStartsWithLabel)
        {
            matchedLabelLineNumber -= 1;
        }
        
        var matches = new List<DocumentLine>();
        List<DocumentLine> numberLines;
        
        foreach (var previousLine in modifiedPreviousLines.OrderByDescending(line => line.LineNumber))
        {
            if (Number.AnyIsNumber([previousLine], out numberLines))
            {
                matches.Add(numberLines.First());
            }
        }
        
        if (Number.AnyIsNumber([request.line], out numberLines))
        {
            matches.Add(numberLines.First());
        }
        
        foreach (var nextLine in modifiedNextLines.OrderBy(line => line.LineNumber))
        {
            if (Number.AnyIsNumber([nextLine], out numberLines))
            {
                matches.Add(numberLines.First());
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
        
        labelGroupResult.Text = [
            new DocumentLine(
                absoluteMatches.FirstOrDefault()?.Text!,
                PositionConstants.UnknownLineNumber,
                PositionConstants.UnknownPageNumber,
                [],
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate)
        ];
            
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