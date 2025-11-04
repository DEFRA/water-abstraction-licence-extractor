using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Constants;
using WALE.ProcessFile.Models.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class Split
{
    private const int UnknownLinesTotal = -1;
    
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        if (request.label.Text == null || request.label.Text.Count == 0)
        {
            throw new Exception("Incorrect configuration - if position is Split, Text must be set");
        }

        var leftPartLines  = request.previousLines!.Reverse().ToList();

        var nextLine = request.nextLines?.FirstOrDefault();
        
        var lineContainsLabel = LabelMatchingHelper.LineContainsLabel(
            request.line!,
            nextLine,
            request.lineForPosition!,
            request.label.Text,
            LabelPosition.SplitAtLabel,
            UnknownLinesTotal,
            int.MaxValue,
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
                    request.label.Text,
                    LabelPosition.SplitAtLabel,
                    UnknownLinesTotal,
                    int.MaxValue,
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
                    request.label.Text.Select(x => x.Text));
                
                var separateParts = request.line!.Text.Split(splitPhrase);
                var leftPart = separateParts[0].Trim();
                
                var leftPartWords = leftPart
                    .Split(PositionConstants.SpaceChar)
                    .Select(text => new DocumentLineWord(text, null, coords))
                    .ToList();

                var leftColumns = new List<DocumentLineColumn>
                {
                    new(leftPart, leftPartWords)
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
                            null,
                            coords))
                        .ToList();

                    var rightColumns = new List<DocumentLineColumn>
                    {
                        new(rightPart, rightPartWords)
                    };
                    
                    var rightLine = request.line.Clone(rightColumns);
                    rightPartLines = [rightLine];
                }
            }
            else
            {
                rightPartLines.Insert(0, request.line!);

                if (leftPartLines.Count > 0)
                {
                    var lastLeftLine = leftPartLines.Last();
                    
                    rightPartLines.Insert(0, lastLeftLine);
                    leftPartLines.Remove(lastLeftLine);
                }
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
        
        var results = FilterIntoFormat(request, leftPartResult, leftPartLines, false);

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
            
            results.AddRange(FilterIntoFormat(request, rightPartResult, rightPartLines, false));
        }
        
        return await ProcessSubLabelsAsync(request, results);
    }
}