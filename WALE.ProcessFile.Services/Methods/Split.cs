using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

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

        if (request.label.Name == "Dates")
        {
            
        }
        
        var leftPartLines  = request.previousLines!.Reverse().ToList();

        var lineContainsLabel = LabelMatchingHelper.LineContainsLabel(
            request.line!,
            request.label.Text,
            LabelPosition.Split,
            UnknownLinesTotal,
            int.MaxValue,
            out _);        
        
        if (!lineContainsLabel)
        {
            leftPartLines.Add(request.line!);
        }
        
        var rightPartLines = request.nextLines!.ToList();

        if (lineContainsLabel)
        {
            var noPreviousLines = leftPartLines.Count == 0;
            var noNextLines = rightPartLines.Count == 0;
            
            if (noPreviousLines && noNextLines)
            {
                var splitPhrase = string.Join(PositionConstants.SpaceChar, request.label.Text);
                var separateParts = request.line!.Text.Split(splitPhrase);

                var leftPart = separateParts[0].Trim();
                
                var leftPartWords = leftPart
                    .Split(PositionConstants.SpaceChar)
                    .Select(text => new DocumentLineWord(text, null, []))
                    .ToList();

                leftPartLines = [
                    new DocumentLine(leftPart,
                        request.lineNumber,
                        request.line.PageNumber,
                        leftPartWords,
                        request.line.Top,
                        request.line.TopRounded,
                        request.line.Left,
                        request.line.LeftRounded)
                ];
                
                var rightPart = separateParts.Length >= 2 ? separateParts[1].Trim() : null;

                if (rightPart != null)
                {
                    var rightPartWords = rightPart?
                        .Split(PositionConstants.SpaceChar)
                        .Select(text => new DocumentLineWord(text, null, []))
                        .ToList();

                    rightPartLines =
                    [
                        new DocumentLine(rightPart!,
                            request.lineNumber,
                            request.line.PageNumber,
                            rightPartWords!,
                            request.line.Top,
                            request.line.TopRounded,
                            request.line.Left,
                            request.line.LeftRounded)
                    ];
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

        leftPartLines = FormattingHelper.RemoveMultipleBlankLines(leftPartLines);

        var leftPartResult = request.labelGroupResult.Clone(
            MatchType.NotApplicable,
            LabelPosition.Split,
            request.label,
            leftPartLines);
        
        var results = new List<LabelGroupResult>
        {
            leftPartResult
        };

        rightPartLines = FormattingHelper.RemoveMultipleBlankLines(rightPartLines);
        
        if (rightPartLines.Count > 0)
        {
            var rightPartResult = request.labelGroupResult.Clone(
                MatchType.NotApplicable,
                LabelPosition.Split,
                request.label,
                rightPartLines);
            
            results.Add(rightPartResult);
        }

        foreach (var result in results)
        {
            var subResults = await request.pdfDataExtractorService!.ProcessSubLabelsAsync(
                request.label,
                result.Text!,
                request.isOcr,
                request.serviceName,
                request.labelGroupName!,
                request.licenceMapping!,
                request.previouslyParsedPaths!,
                request.outputFolder!,
                request.useCache);
        
            if (request.label.MinimumSubMatches.HasValue
                && request.label.MinimumSubMatches.Value > subResults.Count)
            {
                return [];
            }

            result.SubResults = subResults;
        }
        
        return results;
    }
}