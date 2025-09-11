using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Methods;

public static class AfterTextContainsAnotherMatch
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var returnListTop = new List<LabelGroupResult>();

        var afterText = request.textBeforeAtAndAfterLabel?
            .FirstOrDefault(x => x.Label?.Position == LabelPosition.LabelIsBeforeTextToFind);
        
        return afterText == null ?
            Task.FromResult(returnListTop)
            : ApplicableToMost.FunctionAsync(request);
    }
}