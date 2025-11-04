using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsAfterTextToFind
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
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

        var returnList = FilterIntoFormat(request, labelGroupResult, modifiedPreviousLines, true);//false);

        foreach (var item in returnList)
        {
            FormattingHelper.RemoveRemoves(item, removedLines);
        }
        
        return ProcessSubLabelsAsync(request, returnList);
    }
}