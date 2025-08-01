using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone(
            MatchType.NearNextLineIsCompany,
            LabelPosition.LabelIsBeforeTextToFind,
            request.label);
        
        var inputLines = request.previousLines!.ToList();
        inputLines.AddRange(request.nextLines!);
        
        var modifiedLines = DataHelper.RemoveExcludes(
            request.label,
            inputLines,
            out var removedLines);
        
        var returnList = FilterIntoFormat(request, labelGroupResult, modifiedLines, false);

        foreach (var item in returnList)
        {
            FormattingHelper.RemoveRemoves(item, removedLines);
        }

        return Task.FromResult(returnList);
    }
}