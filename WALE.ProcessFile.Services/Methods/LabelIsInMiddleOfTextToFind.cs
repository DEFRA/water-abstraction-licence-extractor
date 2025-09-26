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
        
        if (request.textBeforeAtAndAfterLabel!.Count >= 1)
        {
            var beforeOnSameLine = request.textBeforeAtAndAfterLabel![0];

            var clonedLine = request.line!.Clone();
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(beforeOnSameLine.Text!));
            
            inputLines.Add(clonedLine);

            if (request.textBeforeAtAndAfterLabel.Count >= 2)
            {
                var afterOnSameLine = request.textBeforeAtAndAfterLabel![1];
                
                clonedLine = request.line!.Clone();
                clonedLine.Columns.Clear();
                clonedLine.Columns.Add(new DocumentLineColumn(afterOnSameLine.Text!));
                
                inputLines.Add(clonedLine);
            }
        }

        inputLines.AddRange(request.nextLines!);
        
        var modifiedLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            inputLines,
            false,
            true,
            out _,
            out var removedLines);
        
        var returnList = FilterIntoFormat(request, labelGroupResult, modifiedLines, false);

        foreach (var item in returnList)
        {
            FormattingHelper.RemoveRemoves(item, removedLines);
        }
        
        return ProcessSubLabelsAsync(request, returnList);
    }
}