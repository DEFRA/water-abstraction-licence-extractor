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
            .FirstOrDefault(x => x.Label.Position == LabelPosition.LabelIsBeforeTextToFind);

        if (afterText == null)
        {
            return Task.FromResult(returnListTop);
        }
        
        // TODO just look in the after text for more results 
        
        return Task.FromResult(returnListTop);
    }
}