using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsInMiddleOfTextToFind
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone(
            MatchedPosition.EitherSideOfLabel,
            LabelPosition.LabelIsInMiddleOfTextToFind,
            request.label);
        
        var inputLines = request.previousLines!.ToList();
        inputLines.Reverse();
        
        if (request.textBeforeAtAndAfterLabel!.Count >= 1)
        {
            var beforeOnSameLine = request.textBeforeAtAndAfterLabel![0];

            var beforeOnSameLineWords = request.line!.Columns
                .SelectMany(c => c.Words)
                .ToList();
            
            beforeOnSameLineWords = DocumentLineColumn.FilterWordsFromText(
                beforeOnSameLineWords,
                beforeOnSameLine.ColumnsText![0]);
            
            var clonedLine = request.line!.Clone();
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(beforeOnSameLineWords));
            
            inputLines.Add(clonedLine);

            if (request.textBeforeAtAndAfterLabel.Count >= 2)
            {
                var afterOnSameLine = request.textBeforeAtAndAfterLabel![1];
                
                var afterOnSameLineWords = request.line.Columns
                    .SelectMany(c => c.Words)
                    .ToList();
                
                afterOnSameLineWords = DocumentLineColumn.FilterWordsFromText(
                    afterOnSameLineWords,
                    afterOnSameLine.ColumnsText![0]);
                
                clonedLine = request.line!.Clone();
                clonedLine.Columns.Clear();
                clonedLine.Columns.Add(new DocumentLineColumn(afterOnSameLineWords));
                
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
        
        return await ProcessSubLabelsAsync(request, returnList);
    }
}