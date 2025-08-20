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
        
        var betweenText = GetTextBetween(
            request.label.TextEnd!,
            request.label.MustContain,
            request.textBeforeAndAfterLabel!.LastOrDefault(
                tuple => tuple.Label.Position is LabelPosition.LabelIsBeforeTextToFind
                    or LabelPosition.TextToFindIsBetweenLabels).Text,
            linesToUse,
            request.lineNumber,
            request.line!,
            labelLineAlreadyIncluded,
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
        
        betweenText = betweenText
            .Where(betweenLine => !DataHelper.IsCorruptedText(betweenLine.Text))
            .ToList();
        
        betweenText = DataHelper.RemoveExcludesAndNotContains(request.label, betweenText, out var removedLines);
        
        labelGroupResult.Text = betweenText.ToList();
        labelGroupResult.MatchType = MatchType.Between;
        labelGroupResult.MatchedLabel = request.label.Clone();

        try
        {
            labelGroupResult.MatchedLabel.TextEnd =
            [
                labelGroupResult.MatchedLabel.TextEnd!.Single(x =>
                    matchedEndText != null
                    && (x.Text == matchedEndText.Value.matchedEndText.Text
                        || x.Text == matchedEndText.Value.matchedEndText.Text + PositionConstants.EndOfLineMarker))
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
        IReadOnlyList<string>? containsText,
        string? firstLineTextAfterLabel,
        IReadOnlyList<DocumentLine> lines,
        int startLineNumber,
        DocumentLine lineInput,
        bool labelLineAlreadyIncluded,
        out (TextToMatch matchedEndText, string matchedContainsText)? matchData)
    {
        matchData = null;
        var foundEndTag = false;
        
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

        var textEndList = textEnd.Select(x => new TextToMatch(x.Text)).ToList();
        var labelMatchCount = new Dictionary<string, int>();
        
        linesLoop.AddRange(lines);
        var totalLines = linesLoop.Count;
        
        foreach (var line in linesLoop)
        {
            var label = new LabelToMatch
            {
                Text = textEndList
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
                        var t = line.Text[..line.Text.IndexOf(matchedEndTextTemp.Text, StringComparison.Ordinal)];
                        
                        var clonedLine2 = line.Clone();
                        clonedLine2.Columns.Clear();
                        clonedLine2.Columns.Add(new DocumentLineColumn(
                            FormattingHelper.TrimFormatting(t, false)!));
                        
                        returnList.Add(clonedLine2);
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

        if (!foundEndTag && textEndList.Select(x => x.Text).Contains(PositionConstants.EndOfBlockMarker))
        {
            matchData = (new TextToMatch(PositionConstants.EndOfBlockMarker), PositionConstants.ReplacementMarker);
            foundEndTag = true;
        }

        if (containsText == null)
        {
            return matchData == null ? null : returnList;
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