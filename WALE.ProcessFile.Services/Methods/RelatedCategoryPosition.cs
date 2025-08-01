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

        var matchedLabelLineNumber = PositionConstants.UnknownLineNumber;
        
        foreach (var categoryItem in categoryItems)
        {
            if (categoryItem.MatchedLabel?.Name != request.label.RelatedName)
            {
                continue;
            }
            
            matchedLabelLineNumber = categoryItem.LineNumber;
            break;
        }

        var matches = new List<DocumentLine>();

        foreach (var previousLine in request.previousLines!.OrderByDescending(line => line.LineNumber))
        {
            if (Number.AnyIsNumber([previousLine], out var numberLines))
            {
                matches.Add(numberLines.First());
            }
        }
        
        foreach (var nextLine in request.nextLines!.OrderBy(line => line.LineNumber))
        {
            if (Number.AnyIsNumber([nextLine], out var numberLines))
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
                PositionConstants.UnknownCoOrdinate,
                PositionConstants.UnknownCoOrdinate,
                PositionConstants.UnknownCoOrdinate,
                PositionConstants.UnknownCoOrdinate)
        ];
            
        labelGroupResult.MatchedLabel = request.label;

        // TODO should set match type
        FormattingHelper.RemoveRemoves(labelGroupResult, []); // TODO probably do something else

        returnList.Add(labelGroupResult);

        return Task.FromResult(FilterIntoFormat(
            request,
            labelGroupResult,
            absoluteMatches.Take(1).ToList(),
            false));
    }
}