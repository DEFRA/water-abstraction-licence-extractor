using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

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
            request.label.TextToMatch,
            request.label.Position,
            0,
            0,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _,
            out _);
        
        var labelLineAlreadyIncluded = false;
        var lineContainsSomethingOtherThenJustLabel = request.line?.Text != request.label.TextToMatch?.FirstOrDefault()?.Text;
        
        if (lineContainsLabel != true || (request.label.IncludeWholeLine && lineContainsSomethingOtherThenJustLabel))
        {
            labelLineAlreadyIncluded = true;
            linesToUse.Add(request.line!);
        }

        linesToUse.AddRange(request.nextLines!);

        var relevantLineText = DataHelper.GetTextBeforeAtAndAfterLabelAsSingleString(
            request.textBeforeAtAndAfterLabel,
            false); // We will re-add start label text later if needed
        
        var beforeTextContainsLabel = request.label.TextToMatch?.Any(labelText =>
            (labelText is { LineMustStartWith: false, ColumnMustStartWith: false }
             && relevantLineText.Contains(labelText.Text, StringComparison.OrdinalIgnoreCase))
            || ((labelText.LineMustStartWith || labelText.ColumnMustStartWith)
                    && relevantLineText.StartsWith(labelText.Text, StringComparison.OrdinalIgnoreCase)));

        var textEnd = request.label.TextEnd!.ToList();
        
        if (request.label.IncludeStartLabelText
            && !labelLineAlreadyIncluded
            && beforeTextContainsLabel != true)
        {
            var labelText1 = request.textBeforeAtAndAfterLabel?
                .FirstOrDefault(x => x.Label?.Position == LabelPosition.LabelIsActuallyResult)?
                .ColumnsText![0];

            // Remove the one we matched on in TextStart from TextEnd (or any subset of it)
            if (!string.IsNullOrEmpty(labelText1) && labelText1 != "[START_OF_BLOCK]")
            {
                relevantLineText = $"{labelText1}{relevantLineText}";
                
                textEnd = textEnd.Where(te =>
                {
                    var teTextWithoutMarkers = te.Text
                        .Replace(PositionConstants.EndOfColumnMarker, string.Empty)
                        .Replace(PositionConstants.EndOfLineMarker, string.Empty);
                    
                    return !labelText1.Contains(teTextWithoutMarkers, StringComparison.OrdinalIgnoreCase);
                }).ToList();
            }
        }
        
        var betweenText = GetTextBetween(
            textEnd,
            relevantLineText,
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
        
        if (request.label.IncludeEndLabelText)
        {
            var endText = matchedEndText?.matchedEndText.Text;
            var wordsToAdd = DocumentLineColumn.TextToWords(endText!, null);

            var existingWords = betweenText.LastOrDefault()?.Columns.LastOrDefault()?.Words!;
            existingWords.AddRange(wordsToAdd);
        }

        if (request.label.MustContain?.Count > 0)
        {
            var containsText = request.label.MustContain;
            string? matchedContains = null;
        
            var result = foundEndTag && containsText.Any(containsInstance =>
            {
                var matchResult = string.IsNullOrEmpty(containsInstance) || betweenText.Any(line =>
                    line.Text.Contains(containsInstance, StringComparison.OrdinalIgnoreCase));

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
            .Where(betweenLine => !DataHelper.IsCorruptedLine(betweenLine.Text, request.isOcr))
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
        labelGroupResult.MatchedPosition = MatchedPosition.BetweenLabels;
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
        
        var returnList = await FilterIntoFormatAsync(request, labelGroupResult, betweenText, false);
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
            
            var textWords = lineInput.Columns
                .SelectMany(c => c.Words)
                .ToList();
            
            textWords = DocumentLineColumn.FilterWordsFromText(textWords, text);
            
            var clonedLine = lineInput.Clone();
            clonedLine.LineNumber = startLineNumber;
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(textWords));
            
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
                label.TextToMatch,
                label.Position,
                lineCount++,
                totalLines,
                out var matchedEndTextTemp,
                out _,
                out _,
                out _,
                out _,
                out _,
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
                        var combinedText = !string.IsNullOrEmpty(nextLine?.Text)
                            ? $"{line.Text} {nextLine.Text}"
                            : line.Text;
                        
                        var i = combinedText.IndexOf(matchedEndTextTemp.Text, StringComparison.Ordinal);

                        // TOOD this should look at which actually matched, not just the first label to end on
                        if (label.TextToMatch?.FirstOrDefault()?.Text == PositionConstants.EndOfLineMarker
                            && matchedEndTextTemp.Text == string.Empty)
                        {
                            i = combinedText.Length;
                        }
                        
                        if (i > -1)
                        {
                            var t = combinedText[..i];
                            var ct = FormattingHelper.TrimFormatting(t, !doNotTrimLines, !doNotTrimLines);

                            var isOneDigitNumber = ct?.Length == 1 && int.TryParse(ct, out _);
                            var isOneDigitNumberAndWeDontWantNumber = isOneDigitNumber && label.Format != "Number";

                            if (!isOneDigitNumberAndWeDontWantNumber)
                            {
                                var ctWords = line.Columns
                                    .SelectMany(c => c.Words)
                                    .ToList();

                                if (nextLine != null)
                                {
                                    ctWords.AddRange(nextLine.Columns.SelectMany(c => c.Words));
                                }
                                
                                ctWords = DocumentLineColumn.FilterWordsFromText(ctWords, ct!);
                                
                                var clonedLine2 = line.Clone();
                                clonedLine2.Columns.Clear();
                                clonedLine2.Columns.Add(new DocumentLineColumn(ctWords));

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
                var columnTextWords = DocumentLineColumn.FilterWordsFromText(column.Words, columnText);
                
                clonedLine.Columns.Add(new DocumentLineColumn(columnTextWords));
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