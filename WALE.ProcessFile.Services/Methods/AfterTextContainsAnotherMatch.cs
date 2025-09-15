using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Methods;

public static class AfterTextContainsAnotherMatch
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var returnListTop = new List<LabelGroupResult>();

        var afterText = request.textBeforeAtAndAfterLabel?
            .FirstOrDefault(x => x.Label?.Position == LabelPosition.LabelIsBeforeTextToFind)?.Text;

        if (string.IsNullOrEmpty(afterText))
        {
            return returnListTop;
        }

        var originalText = request.line!.Text;
        request.line?.Columns.Clear();

        if (request.label.Text == null || request.label.Text.Count == 0)
        {
            return returnListTop;
        }

        var asLine = new DocumentLine
        {
            Columns = [new DocumentLineColumn(afterText)]
        };

        var nextLine = request.nextLines?.FirstOrDefault();
        
        if (!LabelMatchingHelper.LineContainsLabel(
            asLine,
            nextLine,
            asLine,
            request.label.Text,
            request.label.Position,
            0,
            PositionConstants.UnknownLinesTotal,
            out _,
            out _))
        {
            return returnListTop;
        }
        
        var results = await ApplicableToMost.FunctionAsync(request);
        
        foreach (var result in results)
        {
            var afterTextInOriginalLinePosition = originalText.IndexOf(afterText,
                StringComparison.InvariantCultureIgnoreCase);
            var valueInAfterTextPosition = afterText.IndexOf(result.Text!.First().Text,
                StringComparison.InvariantCultureIgnoreCase);
            var labelInAfterTextPosition = afterText.IndexOf(result.MatchedLabel!.Text!.First().Text,
                StringComparison.InvariantCultureIgnoreCase);
            
            result.CharPosition = afterTextInOriginalLinePosition + labelInAfterTextPosition;

            if (result.CharPosition == 124)
            {
                
            }
            
            result.MatchedLabel.Position = valueInAfterTextPosition > labelInAfterTextPosition ?
                LabelPosition.LabelIsBeforeTextToFind : LabelPosition.LabelIsAfterTextToFind;
        }
        
        return results;
    }
}