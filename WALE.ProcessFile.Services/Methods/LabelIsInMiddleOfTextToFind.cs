using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsInMiddleOfTextToFind
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone(
            MatchType.MatchIsEitherSideOfLabel,
            LabelPosition.LabelIsInMiddleOfTextToFind,
            request.label);
        
        var inputLines = request.previousLines!.ToList();
        inputLines.Reverse();
        
        if (request.textBeforeAndAfterLabel!.Count >= 1)
        {
            var beforeOnSameLine = request.textBeforeAndAfterLabel![0];
            inputLines.Add(request.line!.Clone(beforeOnSameLine.Text!));

            if (request.textBeforeAndAfterLabel.Count >= 2)
            {
                var afterOnSameLine = request.textBeforeAndAfterLabel![1];
                inputLines.Add(request.line!.Clone(afterOnSameLine.Text!));
            }
        }

        inputLines.AddRange(request.nextLines!);
        
        var modifiedLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            inputLines,
            out var removedLines);
        
        var returnList = FilterIntoFormat(request, labelGroupResult, modifiedLines, false);

        foreach (var item in returnList)
        {
            FormattingHelper.RemoveRemoves(item, removedLines);
        }
        
        return ProcessSubLabelsAsync(request, returnList);
    }
}