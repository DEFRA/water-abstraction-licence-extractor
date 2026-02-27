using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;
using MatchType = WALE.ProcessFile.Core.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class TextToFindIsBetweenLabels
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult;
        
        var linesToUse = new List<DocumentLine>();

        if (request.label.LeewayBefore >= 1
            && request.previousLines!.Count >= request.label.LeewayBefore)
        {
            linesToUse.Add(request.previousLines[^request.label.LeewayBefore]);
        }

        var nextLine = request.nextLines?.FirstOrDefault();

        var lineContainsLabel = LabelMatchingHelper.LineContainsLabel(
            request.line!,
            nextLine,
            request.line!,
            request.label.Text,
            request.label.Position,
            0,
            0,
            out _,
            out _);
        
        var labelLineAlreadyIncluded = false;
        var lineContainsSomethingOtherThenJustLabel = request.line?.Text != request.label.Text?.FirstOrDefault()?.Text;
        
        if (lineContainsLabel != true || (request.label.IncludeWholeLine && lineContainsSomethingOtherThenJustLabel))
        {
            labelLineAlreadyIncluded = true;
            linesToUse.Add(request.line!);
        }

        linesToUse.AddRange(request.nextLines!);

        var lineBeforeText = DataHelper.GetTextBeforeAtAndAfterLabelAsSingleString(
            request.textBeforeAtAndAfterLabel,
            false);
        
        var beforeTextContainsLabel = request.label.Text?.Any(labelText =>
            ((!labelText.LineMustStartWith && !labelText.ColumnMustStartWith)
                && lineBeforeText.Contains(labelText.Text, StringComparison.InvariantCultureIgnoreCase))
            || ((labelText.LineMustStartWith || labelText.ColumnMustStartWith)
                    && lineBeforeText.StartsWith(labelText.Text, StringComparison.InvariantCultureIgnoreCase)));

        var betweenText = GetTextBetween(
            request.label.TextEnd!,
            lineBeforeText,
            linesToUse,
            request.lineNumber,
            request.line!,
            labelLineAlreadyIncluded,
            request.label.DoNotTrimLines,
            out var foundEndTag,
            out var matchedEndText);
        
        if (betweenText == null)
        {
            return [];
        }
        
        // Add label text if asked for
        if (request.label.IncludeStartLabelText
            && betweenText.Count >= 1
            && !labelLineAlreadyIncluded
            && beforeTextContainsLabel != true)
        {
            var labelText = request.textBeforeAtAndAfterLabel?
                .FirstOrDefault(x => x.Label?.Position == LabelPosition.ActuallyLabel)?
                .ColumnsText![0];

            if (!string.IsNullOrEmpty(labelText) && labelText != "[START_OF_BLOCK]")
            {
                var firstBetweenLine = betweenText[0];
                var firstColumn = firstBetweenLine.Columns.Count > 0 ? firstBetweenLine.Columns[0] : null;
                var firstColumnText = firstColumn != null ?
                    FormattingHelper.TrimFormatting(firstColumn.Text, true, false) : null;
                var text = labelText;

                if (!string.IsNullOrEmpty(firstColumnText))
                {
                    text += $" {firstColumnText}";
                }
                
                if (request.label.IncludeEndLabelText)
                {
                    var endText = matchedEndText?.matchedEndText.Text;
                    text += $" {endText}";
                }

                if (betweenText[0].Columns.Count == 0)
                {
                    betweenText[0].Columns.Add(new DocumentLineColumn(text));
                }
                else
                {
                    betweenText[0].Columns[0] = new DocumentLineColumn(text);   
                }
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
            return [];
        }
        
        betweenText = betweenText
            .Where(betweenLine => !DataHelper.IsCorruptedText(betweenLine.Text, request.isOcr))
            .ToList();
        
        betweenText = DataHelper.RemoveExcludesAndNotContains(
            request.label,
            betweenText,
            true,
            false,
            out var isForbidden,
            out var removedLines);
        
        if (isForbidden && betweenText.Count == 0)
        {
            return [];
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
            ConsoleHelper.WriteLine($"ERROR - TextToFindIsBetweenLabels - {e}");
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
        return await ProcessSubLabelsAsync(request, returnList);
    }
    
    private static List<DocumentLine>? GetTextBetween(
        IReadOnlyList<TextToMatch> textEnd,
        string? firstLineTextAfterLabel,
        IReadOnlyList<DocumentLine> lines,
        int startLineNumber,
        DocumentLine lineInput,
        bool labelLineAlreadyIncluded,
        bool doNotTrimLines,
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
                TrimFormatting(firstLineTextAfterLabel, false, false)!;
            
            var clonedLine = lineInput.Clone();
            clonedLine.LineNumber = startLineNumber;
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(text));
            
            linesLoop.Add(clonedLine);
        }
        
        var labelMatchCount = new Dictionary<string, int>();
        
        linesLoop.AddRange(lines);
        var totalLines = linesLoop.Count;

        var count = 0;
        
        foreach (var line in linesLoop)
        {
            var label = new LabelToMatch
            {
                Text = textEnd
            };

            var nextLine = linesLoop.Count > count + 1 ?
                linesLoop[count + 1]
                : null;

            count += 1;
            
            var lineContainsLabel = LabelMatchingHelper.LineContainsLabel(
                line,
                nextLine,
                line,
                label.Text,
                label.Position,
                lineCount++,
                totalLines,
                out var matchedEndTextTemp,
                out _);

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
                    ConsoleHelper.WriteLine($"ERROR - TextToFindIsBetweenLabels - {e}");
                    throw;
                }
                
                if (labelMatchCount[matchedEndTextTemp.Text] >= requiredCount)
                {
                    matchData = (matchedEndTextTemp, PositionConstants.ReplacementMarker);
                    foundEndTag = true;

                    if (returnList.Count == 0 || line.Columns.Count == 1)
                    {
                        var i = line.Text.IndexOf(matchedEndTextTemp.Text, StringComparison.Ordinal);

                        if (i > -1)
                        {
                            var t = line.Text[..i];
                            var ct = FormattingHelper.TrimFormatting(t, !doNotTrimLines, !doNotTrimLines);

                            var isOneDigitNumber = ct?.Length == 1 && int.TryParse(ct, out _);
                            var isOneDigitNumberAndWeDontWantNumber = isOneDigitNumber && label.Format != "Number";

                            if (!isOneDigitNumberAndWeDontWantNumber)
                            {
                                var clonedLine2 = line.Clone();
                                clonedLine2.Columns.Clear();
                                clonedLine2.Columns.Add(new DocumentLineColumn(ct!));

                                returnList.Add(clonedLine2);
                            }
                        }
                    }
                    
                    break;
                }
            }
            
            var clonedLine = line.Clone();
            clonedLine.Columns.Clear();

            foreach (var column in line.Columns)
            {
                var columnText = FormattingHelper.TrimFormatting(column.Text, false, false)!;
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