using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;
using MatchType = WALE.ProcessFile.Core.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsBeforeTextToFind
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone(
            MatchType.NearNextLineIsMatch,
            LabelPosition.LabelIsBeforeTextToFind,
            request.label);

        var modifiedNextLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            request.nextLines,
            false,
            true,
            out _,
            out var removedLines);

        var returnList = await FilterIntoFormatAsync(request, labelGroupResult, modifiedNextLines, false);
        
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