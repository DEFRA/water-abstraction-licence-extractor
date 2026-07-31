using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class SplitAtLabel
{
    private const int UnknownLinesTotal = -1;
    
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        if (request.label.Name == "AbstractionLimitPointSub")
        {
            
        }
        
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        if (request.label.TextToMatch == null || request.label.TextToMatch.Count == 0)
        {
            throw new Exception("Incorrect configuration - if position is Split, Text must be set");
        }

        var leftPartLines  = request.previousLines!.Reverse().ToList();

        var nextLine = request.nextLines?.FirstOrDefault();
        
        var lineContainsLabel = LabelMatchingHelper.LineContainsLabel(
            request.line!,
            nextLine,
            request.lineForPosition!,
            request.label.TextToMatch,
            LabelPosition.SplitAtLabel,
            UnknownLinesTotal,
            int.MaxValue,
            out _,
            out _,
            out _,
            out _,          
            out _,
            out _,
            out _);
        
        if (!lineContainsLabel)
        {
            leftPartLines.Add(request.line!);
        }

        if (!lineContainsLabel)
        {
            foreach (var line in leftPartLines)
            {
                lineContainsLabel = LabelMatchingHelper.LineContainsLabel(
                    line,
                    null,
                    line,
                    request.label.TextToMatch,
                    LabelPosition.SplitAtLabel,
                    UnknownLinesTotal,
                    int.MaxValue,
                    out _,
                    out _,
                    out _,
                    out _,          
                    out _,
                    out _,
                    out _);

                if (lineContainsLabel)
                {
                    break;
                }
            }
        }

        var rightPartLines = request.nextLines!.ToList();

        if (lineContainsLabel)
        {
            var noPreviousLines = leftPartLines.Count == 0;
            var noNextLines = rightPartLines.Count == 0;

            var coords = request.line!
                .Columns
                .FirstOrDefault()!
                .Words
                .FirstOrDefault()!
                .Coordinates;
            
            if (noPreviousLines || noNextLines)
            {
                var splitPhrase = string.Join(
                    PositionConstants.SpaceChar,
                    request.label.TextToMatch.Select(x => x.Text));
                
                var separateParts = request.line!.Text.Split(splitPhrase);
                var leftPart = separateParts[0].Trim();
                
                var leftPartWords = leftPart
                    .Split(PositionConstants.SpaceChar)
                    .Select(text => new DocumentLineWord(text, request.line.OcrConfidence, coords, null))
                    .ToList();

                var leftColumns = new List<DocumentLineColumn>
                {
                    new(leftPartWords)
                };

                var leftLine = request.line.Clone(leftColumns);

                leftPartLines.Remove(leftLine);
                leftPartLines.Add(leftLine);
                
                var rightPart = separateParts.Length >= 2 ? separateParts[1].Trim() : null;

                if (rightPart != null)
                {
                    var rightPartWords = rightPart
                        .Split(PositionConstants.SpaceChar)
                        .Select(text => new DocumentLineWord(
                            text,
                            request.line.OcrConfidence,
                            coords,
                            null))
                        .ToList();

                    var rightColumns = new List<DocumentLineColumn>
                    {
                        new(rightPartWords)
                    };
                    
                    var rightLine = request.line.Clone(rightColumns);
                    rightPartLines = [rightLine];
                }
            }
            else
            {
                rightPartLines.Insert(0, request.line!);
            }
        }
        
        leftPartLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            leftPartLines, 
            false,
            !request.label.DoNotTrimLines,
            out _, 
            out _);
        
        leftPartLines = FormattingHelper.RemoveMultipleBlankLines(leftPartLines);

        var leftPartResult = request.labelGroupResult.Clone(
            MatchedPosition.NotApplicable,
            LabelPosition.SplitAtLabel,
            request.label,
            leftPartLines);
        
        var results = await FilterIntoFormatAsync(request, leftPartResult, leftPartLines, false);

        rightPartLines = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            rightPartLines, 
            false,
            true,
            out _,
            out _);
        
        rightPartLines = FormattingHelper.RemoveMultipleBlankLines(rightPartLines);
        
        if (rightPartLines.Count > 0)
        {
            var rightPartResult = request.labelGroupResult.Clone(
                MatchedPosition.NotApplicable,
                LabelPosition.SplitAtLabel,
                request.label,
                rightPartLines);
            
            results.AddRange(await FilterIntoFormatAsync(request, rightPartResult, rightPartLines, false));
        }
        
        return await ProcessSubLabelsAsync(request, results);
    }
}