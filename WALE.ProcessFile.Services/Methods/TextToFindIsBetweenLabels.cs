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
        
        var labelGroupResult = request.labelGroupResult;//.Clone();
        
        var linesToUse = new List<DocumentLine>();

        if (request.label.LeewayBefore >= 1
            && request.previousLines!.Count >= request.label.LeewayBefore)
        {
            linesToUse.Add(request.previousLines[^request.label.LeewayBefore]);
        }

        var lineContainsLabel = request.label.Text?.Any(labelText =>
            request.line!.Text.Contains(labelText.Text, StringComparison.InvariantCultureIgnoreCase));

        var labelLineAlreadyIncluded = false;
        
        if (lineContainsLabel != true || request.label.IncludeWholeLine)
        {
            labelLineAlreadyIncluded = true;
            linesToUse.Add(request.line!);
        }

        linesToUse.AddRange(request.nextLines!);
        
        if (request.label.Name == "PointCondition")
        {
            
        }
        
        var betweenText = GetTextBetween(
            request.label.TextEnd!,
            request.textBeforeAtAndAfterLabel!.LastOrDefault(
                tuple => tuple.Label.Position is LabelPosition.LabelIsBeforeTextToFind
                    or LabelPosition.TextToFindIsBetweenLabels).Text,
            linesToUse,
            request.lineNumber,
            request.line!,
            labelLineAlreadyIncluded,
            out var foundEndTag,
            out var matchedEndText);
        
        if (betweenText == null)
        {
            return Task.FromResult(new List<LabelGroupResult>());
        }

        // Add label text if asked for
        if (request.label.IncludeLabelText
            && betweenText.Count >= 1
            && !labelLineAlreadyIncluded)
        {
            var firstBetweenLine = betweenText[0];
            var firstColumn = firstBetweenLine.Columns[0];
            
            var labelText = request.label.Text!.FirstOrDefault()?.Text;

            if (labelText != "[START_OF_BLOCK]")
            {
                var text = $"{labelText!} {firstColumn.Text}";
                betweenText[0].Columns[0] = new DocumentLineColumn(text);   
            }
        }

        if (request.label.MustContain?.Count > 0)
        {
            var containsText = request.label.MustContain;
            string? matchedContains = null;
        
            var result = foundEndTag && containsText.Any(containsInstance =>
            {
                var matchResult = string.IsNullOrEmpty(containsInstance) || betweenText.Any(line =>
                    line.Text.Contains(containsInstance, StringComparison.InvariantCultureIgnoreCase));

                if (!matchResult)
                {
                    return false;
                }
            
                matchedContains = containsInstance;
                return true;

            }) ? betweenText : null;

            if (matchedContains != null)
            {
                matchedEndText = (matchedEndText!.Value.matchedEndText, matchedContains!);
                betweenText = result;
            }
            else
            {
                matchedEndText = null;
                betweenText = result;
            }
        }
        
        if (betweenText == null)
        {
            return Task.FromResult(new List<LabelGroupResult>());
        }
        
        betweenText = betweenText
            .Where(betweenLine => !DataHelper.IsCorruptedText(betweenLine.Text))
            .ToList();
        
        betweenText = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            betweenText,
            true,
            out var isForbidden,
            out var removedLines);
        
        if (isForbidden && betweenText.Count == 0)
        {
            return Task.FromResult(new List<LabelGroupResult>());
        }
        
        labelGroupResult.Text = betweenText.ToList();
        labelGroupResult.MatchType = MatchType.Between;
        labelGroupResult.MatchedLabel = request.label.Clone();

        try
        {
            labelGroupResult.MatchedLabel.TextEnd =
            [
                labelGroupResult.MatchedLabel.TextEnd!.Single(textEndLine =>
                    matchedEndText != null
                    && (textEndLine.Text == matchedEndText.Value.matchedEndText.Text
                        || textEndLine.Text == matchedEndText.Value.matchedEndText.Text + PositionConstants.EndOfColumnMarker
                        || textEndLine.Text == matchedEndText.Value.matchedEndText.Text + PositionConstants.EndOfLineMarker))
            ];
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }

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
        IReadOnlyList<TextToMatch> textEnd,
        string? firstLineTextAfterLabel,
        IReadOnlyList<DocumentLine> lines,
        int startLineNumber,
        DocumentLine lineInput,
        bool labelLineAlreadyIncluded,
        out bool foundEndTag,
        out (TextToMatch matchedEndText, string matchedContainsText)? matchData)
    {
        matchData = null;
        foundEndTag = false;
        
        var lineCount = 0;
        var returnList = new List<DocumentLine>();
        var linesLoop = new List<DocumentLine>();
        
        // Add the first line between
        if (!string.IsNullOrEmpty(firstLineTextAfterLabel) && !labelLineAlreadyIncluded)
        {
            var text = FormattingHelper.
                TrimFormatting(firstLineTextAfterLabel, true)!;
            
            var clonedLine = lineInput.Clone();
            clonedLine.LineNumber = startLineNumber;
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(text));
            
            linesLoop.Add(clonedLine);
        }
        
        var labelMatchCount = new Dictionary<string, int>();
        
        linesLoop.AddRange(lines);
        var totalLines = linesLoop.Count;
        
        foreach (var line in linesLoop)
        {
            var label = new LabelToMatch
            {
                Text = textEnd
            };

            var lineContainsLabel = LabelMatchingHelper.LineContainsLabel(
                line,
                label.Text,
                label.Position,
                lineCount++,
                totalLines,
                out var matchedEndTextTemp);

            if (lineContainsLabel)
            {
                labelMatchCount.TryAdd(matchedEndTextTemp!.Text, 0);
                labelMatchCount[matchedEndTextTemp.Text] += 1;

                int requiredCount;

                try
                {
                    requiredCount = textEnd
                        .Single(textEndLine => textEndLine.Text == matchedEndTextTemp.Text
                            || textEndLine.Text == matchedEndTextTemp.Text + PositionConstants.EndOfColumnMarker
                            || textEndLine.Text == matchedEndTextTemp.Text + PositionConstants.EndOfLineMarker)
                        .InstanceNumber;
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    throw;
                }
                
                if (labelMatchCount[matchedEndTextTemp.Text] >= requiredCount)
                {
                    matchData = (matchedEndTextTemp, PositionConstants.ReplacementMarker);
                    foundEndTag = true;

                    if (returnList.Count == 0)
                    {
                        var i = line.Text.IndexOf(matchedEndTextTemp.Text, StringComparison.Ordinal);

                        if (i > -1)
                        {
                            var t = line.Text[..i];

                            var clonedLine2 = line.Clone();
                            clonedLine2.Columns.Clear();
                            clonedLine2.Columns.Add(new DocumentLineColumn(
                                FormattingHelper.TrimFormatting(t, false)!));

                            returnList.Add(clonedLine2);
                        }
                    }
                    
                    break;
                }
            }
            
            var clonedLine = line.Clone();
            clonedLine.Columns.Clear();

            foreach (var column in line.Columns)
            {
                var isLastColumn = line.Columns.Last() == column;
                
                var columnText = FormattingHelper.TrimFormatting(column.Text, isLastColumn)!;
                clonedLine.Columns.Add(new DocumentLineColumn(columnText));
            }
            
            returnList.Add(clonedLine);
        }

        if (!foundEndTag && textEnd.Select(x => x.Text).Contains(PositionConstants.EndOfBlockMarker))
        {
            matchData = (new TextToMatch(PositionConstants.EndOfBlockMarker), PositionConstants.ReplacementMarker);
            foundEndTag = true;
        }

        return matchData == null ? null : returnList;
    }
}