using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class TextToFindIsBetweenLabels
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone();
        
        var linesToUse = new List<DocumentLine>();

        if (request.label.LeewayBefore >= 1
            && request.previousLines!.Count >= request.label.LeewayBefore)
        {
            linesToUse.Add(request.previousLines[^request.label.LeewayBefore]);
        }

        var lineContainsLabel = request.label.Text?.Any(labelText =>
            request.line!.Text.Contains(labelText, StringComparison.InvariantCultureIgnoreCase));

        if (lineContainsLabel != true)
        {
            linesToUse.Add(request.line!);                        
        }
        
        linesToUse.AddRange(request.nextLines!);
        
        var betweenText = GetTextBetween(
            request.label.TextEnd!,
            request.label.MustContain,
            request.textBeforeAndAfterLabel!.LastOrDefault(
                tuple => tuple.Label.Position is LabelPosition.LabelIsBeforeTextToFind
                    or LabelPosition.TextToFindIsBetweenLabels).Text,
            linesToUse,
            request.lineNumber,
            request.line!,
            out var matchedEndText);
        
        if (betweenText == null)
        {
            return Task.FromResult(new List<LabelGroupResult>());
        }

        if (request.label.IncludeLabelText && betweenText.Count >= 1)
        {
            var labelText = request.label.Text!.FirstOrDefault()!;

            if (labelText != "[START_OF_BLOCK]")
            {
                betweenText[0] = betweenText[0].Clone(
                    request.label.Text!.FirstOrDefault()! + " " + betweenText[0].Text);   
            }
        }
        
        betweenText = betweenText
            .Where(betweenLine => !DataHelper.IsCorruptedText(betweenLine.Text))
            .ToList();
        
        betweenText = DataHelper.RemoveExcludesAndNotContains(request.label, betweenText, out var removedLines);
        
        labelGroupResult.Text = betweenText.ToList();
        labelGroupResult.MatchType = MatchType.Between;
        labelGroupResult.MatchedLabel = request.label.Clone();
        labelGroupResult.MatchedLabel.TextEnd =
        [
            labelGroupResult.MatchedLabel.TextEnd!.Single(x =>
                matchedEndText != null
                && x == matchedEndText.Value.matchedEndText)
        ];

        if (labelGroupResult.MatchedLabel.MustContain != null)
        {
            labelGroupResult.MatchedLabel.MustContain =
            [
                labelGroupResult.MatchedLabel.MustContain!.Single(x =>
                    matchedEndText != null && x == matchedEndText.Value.matchedContainsText)
            ];
        }

        FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
        
        var returnList = FilterIntoFormat(request, labelGroupResult, betweenText, false);
        return ProcessSubLabelsAsync(request, returnList);
    }
    
    private static List<DocumentLine>? GetTextBetween(
        IReadOnlyList<string> textEnd,
        IReadOnlyList<string>? containsText,
        string? firstLineTextAfterLabel,
        IReadOnlyList<DocumentLine> nextLines,
        int startLineNumber,
        DocumentLine lineInput,
        out (string matchedEndText, string matchedContainsText)? matchData)
    {
        matchData = null;
        var foundEndTag = false;
        
        var lineCount = 0;
        var returnList = new List<DocumentLine>();
        
        if (!string.IsNullOrEmpty(firstLineTextAfterLabel))
        {
            var clonedLine = lineInput.Clone(FormattingHelper.TrimFormatting(firstLineTextAfterLabel)!);
            clonedLine.LineNumber = startLineNumber;
            
            returnList.Add(clonedLine);
        }

        var totalLines = nextLines.Count;
        
        foreach (var line in nextLines)
        {
            var label = new LabelToMatch
            {
                Text = textEnd
            };
            
            if (LabelMatchingHelper.LineContainsLabel(
                line,
                label.Text,
                label.Position,
                lineCount++,
                totalLines,
                out var matchedEndTextTemp))
            {
                matchData = (matchedEndTextTemp!, PositionConstants.ReplacementMarker);
                foundEndTag = true;

                break;
            }
            
            var text = FormattingHelper.TrimFormatting(line.Text)!;
            returnList.Add(line.Clone(text));
        }

        if (!foundEndTag && textEnd.Contains(PositionConstants.EndOfBlockMarker))
        {
            matchData = (PositionConstants.EndOfBlockMarker, PositionConstants.ReplacementMarker);
            foundEndTag = true;
        }

        if (containsText == null)
        {
            return returnList;
        }

        string? matchedContains = null;
        
        var result = foundEndTag && containsText.Any(containsInstance =>
        {
            var matchResult = string.IsNullOrEmpty(containsInstance) || returnList.Any(line =>
                line.Text.Contains(containsInstance, StringComparison.InvariantCultureIgnoreCase));

            if (!matchResult)
            {
                return false;
            }
            
            matchedContains = containsInstance;
            return true;

        }) ? returnList : null;

        if (matchedContains != null)
        {
            matchData = (matchData!.Value.matchedEndText, matchedContains!);
            return result;
        }

        matchData = null;
        return result;
    }
}