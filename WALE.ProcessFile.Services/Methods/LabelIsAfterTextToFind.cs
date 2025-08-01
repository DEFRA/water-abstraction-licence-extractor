using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsAfterTextToFind
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);

        var labelGroupResult = request.labelGroupResult.Clone(
            MatchType.NearPreviousLineIsCompany,
            LabelPosition.LabelIsAfterTextToFind,
            request.label);
        
        var modifiedPreviousLines = DataHelper.RemoveExcludes(
            request.label,
            request.previousLines,
            out var removedLines);

        var returnList = FilterIntoFormat(request, labelGroupResult, modifiedPreviousLines, true);

        foreach (var item in returnList)
        {
            FormattingHelper.RemoveRemoves(item, removedLines);
        }
        
        return Task.FromResult(returnList);
    }
}