using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
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
            .FirstOrDefault(x => x.Label?.Position == LabelPosition.LabelIsBeforeTextToFind)?.ColumnsText?[0];

        if (string.IsNullOrEmpty(afterText))
        {
            return returnListTop;
        }

        var originalWords = request.line!.Columns
            .SelectMany(c => c.Words)
            .ToList();
        
        var originalText = request.line.Text;
        request.line?.Columns.Clear();

        if (request.label.TextToMatch == null || request.label.TextToMatch.Count == 0)
        {
            return returnListTop;
        }
        
        var afterTextWords = DocumentLineColumn.FilterWordsFromText(
            originalWords,
            afterText);

        var asLine = new DocumentLine
        {
            Columns = [new DocumentLineColumn(afterTextWords)],
            PageNumber = request.line!.PageNumber,
            LineNumber = request.line.LineNumber
        };

        var nextLine = request.nextLines?.FirstOrDefault();
        
        if (!LabelMatchingHelper.LineContainsLabel(
            asLine,
            nextLine,
            asLine,
            request.label.TextToMatch,
            request.label.Position,
            0,
            PositionConstants.UnknownLinesTotal,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _))
        {
            return returnListTop;
        }
        
        var clonedRequest = request.Clone();
        clonedRequest.line = asLine;
        
        var results = await ApplicableToMost.FunctionAsync(clonedRequest);
        
        foreach (var result in results)
        {
            var afterTextInOriginalLinePosition = originalText.IndexOf(afterText,
                StringComparison.OrdinalIgnoreCase);
            var valueInAfterTextPosition = afterText.IndexOf(result.Text!.First().Text,
                StringComparison.OrdinalIgnoreCase);
            var labelInAfterTextPosition = afterText.IndexOf(result.MatchedLabel!.TextToMatch!.First().Text,
                StringComparison.OrdinalIgnoreCase);
            
            result.LabelStartCharPosition = afterTextInOriginalLinePosition + labelInAfterTextPosition;
            
            result.MatchedLabel.Position = valueInAfterTextPosition > labelInAfterTextPosition ?
                LabelPosition.LabelIsBeforeTextToFind : LabelPosition.LabelIsAfterTextToFind;
        }
        
        return results;
    }
}