using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;

namespace WALE.ProcessFile.Services.Methods;

public static class RelatedCategoryPosition
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        if (request.labelGroupResult == null)
        {
            throw new ArgumentNullException(nameof(request.labelGroupResult));
        }
        
        if (request.label == null)
        {
            throw new ArgumentNullException(nameof(request.label));
        }
        
        var labelGroupResult = request.labelGroupResult.Clone();
                    
        var categoryItems = request.siblingMatches!
            .Where(match => match.MatchedLabel!.CategoryName == request.label.RelatedCategoryName)
            .OrderBy(match => match.LineNumber)
            .ToList();

        var matchedLabelLineNumber = -1;
        
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
            if (AnyIsNumber([previousLine], out var numberLine))
            {
                matches.Add(numberLine!);
            }
        }
        
        foreach (var nextLine in request.nextLines!.OrderBy(line => line.LineNumber))
        {
            if (AnyIsNumber([nextLine], out var numberLine))
            {
                matches.Add(numberLine!);
            }
        }

        var absoluteMatches = matches
            .OrderBy(match => Math.Abs(matchedLabelLineNumber - match.LineNumber))
            .ThenBy(match => match.LineNumber)
            .ToList();

        if (absoluteMatches.Count <= 0)
        {
            return Task.FromResult(new List<LabelGroupResult>());
        }
        
        labelGroupResult.Text = [
            new DocumentLine(
                absoluteMatches.FirstOrDefault()?.Text!,
                -1,
                -1,
                [])
        ];
            
        labelGroupResult.MatchedLabel = request.label;

        // TODO should set match type
        RemoveRemoves(labelGroupResult, []); // TODO probably do something else
            
        return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
    }
}