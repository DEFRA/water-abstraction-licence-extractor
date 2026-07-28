using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsAfterTextToFind
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);

        /*if (LabelMatchingHelper.ContainsForbiddenText(request.line, request.label))
        {
            return ProcessSubLabelsAsync(request, []);
        }*/
        
        var labelGroupResult = request.labelGroupResult.Clone(
            MatchedPosition.OnOrNearPreviousLine,
            LabelPosition.LabelIsAfterTextToFind,
            request.label);
        
        var modifiedPreviousLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.previousLines,
            false,
            true,
            out _,
            out var removedLines);

        var returnList = await FilterIntoFormatAsync(request, labelGroupResult, modifiedPreviousLines, true);

        foreach (var item in returnList)
        {
            FormattingHelper.RemoveRemoves(item, removedLines);
        }
        
        var newReturnList = new List<LabelGroupResult>();
        
        foreach (var item in returnList)
        {
            var text = item.Text?.FirstOrDefault()?.Text;
            
            if (!string.IsNullOrEmpty(text) && LabelMatchingHelper.ShouldSkipResultAsForbidden(text, request.label))
            {
                continue;
            }
            
            newReturnList.Add(item);
        }
        
        return await ProcessSubLabelsAsync(request, newReturnList);
    }
}