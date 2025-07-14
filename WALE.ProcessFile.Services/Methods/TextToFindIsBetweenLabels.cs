using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class TextToFindIsBetweenLabels
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        var label = request.label;
        var labelGroupResult = request.labelGroupResult.Clone();
        
        var linesToUse = new List<DocumentLine>();/*
        {
            line
        };*/

        if (label.Name == "Purpose")
        {
            
        }

        if (label.LeewayBefore >= 1 && request.previousLines.Count >= label.LeewayBefore) // TODO never currently set
        {
            linesToUse.Add(request.previousLines[^label.LeewayBefore]);
        }

        if (label.Text?.Any(t => request.line.Text.Contains(t, StringComparison.InvariantCultureIgnoreCase)) != true)
        {
            linesToUse.Add(request.line);                        
        }
        
        linesToUse.AddRange(request.nextLines);
        
        var betweenText = GetTextBetween(
            label.TextEnd!,
            label.MustContain,
            request.textBeforeAndAfterLabel.LastOrDefault(
                tuple => tuple.Label.Position is LabelPosition.LabelIsBeforeTextToFind
                    or LabelPosition.TextToFindIsBetweenLabels).Text,
            linesToUse,
            request.lineNumber,
            request.line,
            out var matchedEndText);
        
        if (betweenText == null)
        {
            return [];
        }

        if (label.IncludeLabelText && betweenText.Count >= 1)
        {
            betweenText[0] = request.line;
        }
        
        betweenText = betweenText
            .Where(betweenLine => !IsCorruptedText(betweenLine.Text))
            .ToList();

        var subResults = await request.pdfDataExtractorService.ProcessSubLabelsAsync(
            label,
            betweenText,
            request.isOcr,
            request.serviceName,
            request.labelGroupName,
            request.licenceMapping,
            request.previouslyParsedPaths,
            request.outputFolder,
            request.useCache);
        
        if (label.MinimumSubMatches.HasValue && label.MinimumSubMatches.Value > subResults.Count)
        {
            return [];
        }
        
        betweenText = RemoveExcludes(request.label, betweenText, out var removedLines);
        
        labelGroupResult.Text = betweenText.ToList();
        labelGroupResult.MatchType = MatchType.Between;
        labelGroupResult.MatchedLabel = label.Clone();
        labelGroupResult.MatchedLabel.TextEnd =
            [
                labelGroupResult.MatchedLabel.TextEnd!.Single(x => matchedEndText != null && x == matchedEndText.Value.matchedEndText)
            ];

        if (labelGroupResult.MatchedLabel.MustContain != null)
        {
            labelGroupResult.MatchedLabel.MustContain =
            [
                labelGroupResult.MatchedLabel.MustContain!.Single(x =>
                    matchedEndText != null && x == matchedEndText.Value.matchedContainsText)
            ];
        }

        RemoveRemoves(labelGroupResult, removedLines);
        labelGroupResult.SubResults = subResults;

        return [labelGroupResult];
    }
}