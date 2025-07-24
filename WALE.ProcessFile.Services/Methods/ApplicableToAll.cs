using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class ApplicableToAll
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        if (request.labelGroupResult == null)
        {
            throw new ArgumentNullException(nameof(request.labelGroupResult));
        }
        
        if (request.label == null)
        {
            throw new ArgumentNullException(nameof(request.label));
        }

        if (request.label.Name == "PointPointNumber")
        {
            
        }
        
        var labelGroupResult = request.labelGroupResult.Clone();
        var line = request.line;
        var lineNumber = request.lineNumber;
        
        var returnListTop = new List<LabelGroupResult>();
        
        if (!PotentialMatchOnLabelLine(request.textBeforeAndAfterLabel!))
        {
            return returnListTop;
        }
        
        foreach (var (text, matchedLabel) in request.textBeforeAndAfterLabel!)
        {
            var t = matchedLabel.IncludeLabelText ? request.line!.Text : text;
            
            var over2Lines = false;
            var outputText = RemoveExcludes(matchedLabel, t!, out var removedLines);

            if (IsCorruptedText(outputText))
            {
                continue;
            }

            if (request.isDateOrPurposeLookup && IsDateOrPurpose(t))
            {
                labelGroupResult.Text =
                [
                    new DocumentLine(
                        t!,
                        request.line!.LineNumber,
                        request.line.PageNumber,
                        request.line.Words.ToList(),
                        request.line.Top,
                        request.line.TopRounded,
                        request.line.Left,
                        request.line.LeftRounded)
                ];
                
                labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                labelGroupResult.MatchedLabel = matchedLabel;
                RemoveRemoves(labelGroupResult, removedLines);

                return [labelGroupResult];
            }
            
            if (request.isCompanyType
                && (char.IsLower(outputText[0])
                    || outputText.StartsWith("trading as", StringComparison.InvariantCultureIgnoreCase)))
            {
                over2Lines = true;
                outputText = $"{request.previousLines!.FirstOrDefault()?.Text} {outputText}";
            }

            if (request.isNumberLookup
                && TryGetNumber(outputText, line!.LineNumber, line.PageNumber, out var numberLines))
            {
                if (matchedLabel.Possibilities?.Any() == true)
                {
                    var newNumberLines = numberLines
                        .Where(numberLineLoop => matchedLabel.Possibilities.Any(p => p == numberLineLoop.Text))
                        .ToList();

                    numberLines = newNumberLines;
                }
                
                var numberLine = numberLines.FirstOrDefault();

                if (numberLine == null)
                {
                    return [];
                }

                numberLine.Words = line.Words;
                labelGroupResult.Text = [numberLine];
                labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                labelGroupResult.MatchedLabel = matchedLabel;

                RemoveRemoves(labelGroupResult, removedLines);
                return [labelGroupResult];
            }
            
            if (request.isLicenceNumberLookup
                && request.label != null
                && AnyIsLicenceNumber([
                    new DocumentLine(
                        outputText,
                        lineNumber,
                        line!.PageNumber,
                        line.Words.ToList(),
                        line.Top,
                        line.TopRounded,
                        line.Left,
                        line.LeftRounded)],
                    request.label,
                    out var licenceNumberLines))
            {
                var returnList = new List<LabelGroupResult>();
                
                foreach (var licenceNumberLine in licenceNumberLines)
                {
                    labelGroupResult = labelGroupResult.Clone();
                    labelGroupResult.Text = [licenceNumberLine];
                    labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                    labelGroupResult.MatchedLabel = matchedLabel;
                    RemoveRemoves(labelGroupResult, removedLines);
                    
                    returnList.Add(labelGroupResult);
                }

                return returnList;
            }

            if ((request.isSingleWord || request.actsLikeSingleWord) && request.label != null && !string.IsNullOrEmpty(t))
            {
                labelGroupResult.Text =
                [
                    new DocumentLine(
                        request.isSingleWord ? t.Split(' ')[0] : t,
                        line!.LineNumber,
                        line.PageNumber,
                        line.Words.ToList(),
                        line.Top,
                        line.TopRounded,
                        line.Left,
                        line.LeftRounded)
                ];
                
                labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                labelGroupResult.MatchedLabel = matchedLabel;
                RemoveRemoves(labelGroupResult, removedLines);

                var results = new List<LabelGroupResult> {labelGroupResult};
                
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
        
                    if (request.label.MinimumSubMatches.HasValue && request.label.MinimumSubMatches.Value > subResults.Count)
                    {
                        return [];
                    }

                    result.SubResults = subResults;
                }
                
                return results;
            }

            var isPossiblity = false;
            
            if (matchedLabel.Possibilities?.Any() == true)
            {
                var autoCorrectedOutputText = AutoCorrectText(
                    new DocumentLine(
                        outputText,
                        lineNumber,
                        line!.PageNumber,
                        line.Words.ToList(),
                        line.Top,
                        line.TopRounded,
                        line.Left,
                        line.LeftRounded),
                    false);
                
                foreach (var possibility in matchedLabel.Possibilities)
                {
                    if (!outputText.Contains(possibility, StringComparison.InvariantCultureIgnoreCase)
                        && !autoCorrectedOutputText!.Contains(possibility, StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }
                    
                    outputText = possibility;
                    isPossiblity = true;
                    
                    break;
                }
            }
            
            if (request.isUnitsLookup)
            {
                if (matchedLabel.Possibilities == null)
                {
                    continue;
                }
                
                foreach (var possibility in matchedLabel.Possibilities!)
                {
                    if (!outputText.Contains(possibility,
                        StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    labelGroupResult.Text =
                    [
                        new DocumentLine(
                            possibility,
                            line!.LineNumber,
                            line.PageNumber,
                            line.Words.ToList(),
                            line.Top,
                            line.TopRounded,
                            line.Left,
                            line.LeftRounded)
                    ];
                    
                    labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                    labelGroupResult.MatchedLabel = matchedLabel;
                    RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.MatchedLabel.Possibilities = [possibility];
                    
                    return [labelGroupResult];
                }
            }

            outputText = TrimFormatting(outputText);
            outputText = request.isOcr ? AutoCorrectText(new DocumentLine(
                outputText!,
                lineNumber,
                line!.PageNumber,
                line.Words.ToList(),
                line.Top,
                line.TopRounded,
                line.Left,
                line.LeftRounded), request.isCompanyType) : outputText;

            if (request.isCompanyType
                && CompanyName.TryGetCompanyOrPersonalName(new DocumentLine(
                    outputText!,
                    lineNumber,
                    line!.PageNumber,
                    line.Words.ToList(),
                    line.Top,
                    line.TopRounded,
                    line.Left,
                    line.LeftRounded), matchedLabel, out _))
            {
                var matchType = over2Lines ?
                    MatchType.SameLineIsCompany2Lines
                    : MatchType.SameLineIsCompany1Line;
                
                labelGroupResult.Text =
                [
                    new DocumentLine(
                        outputText!,
                        line.LineNumber,
                        line.PageNumber,
                        line.Words.ToList(),
                        line.Top,
                        line.TopRounded,
                        line.Left,
                        line.LeftRounded)
                ];
                
                labelGroupResult.MatchType = matchType;
                labelGroupResult.MatchedLabel = matchedLabel;
                RemoveRemoves(labelGroupResult, removedLines);

                if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                {
                    labelGroupResult.MatchedLabel.Possibilities = [outputText!];   
                }
                
                return [labelGroupResult];
            }

            var trimmedSplit = outputText!.Trim().Split(' ');

            if (trimmedSplit.Length == 1
                && !string.IsNullOrEmpty(trimmedSplit[0])
                && request.isCompanyType)
            {
                labelGroupResult.Text =
                [
                    new DocumentLine(
                        outputText,
                        lineNumber,
                        line!.PageNumber,
                        line.Words.ToList(),
                        line.Top,
                        line.TopRounded,
                        line.Left,
                        line.LeftRounded)
                ];
                
                labelGroupResult.MatchType = MatchType.SameLineSingleWord;
                labelGroupResult.MatchedLabel = matchedLabel;
                RemoveRemoves(labelGroupResult, removedLines);
                
                if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                {
                    labelGroupResult.MatchedLabel.Possibilities = [outputText];   
                }
                
                return [labelGroupResult];
            }

            if (request.label?.Text == null && !string.IsNullOrWhiteSpace(outputText))
            {
                var lineMatch = labelGroupResult.Clone();
                lineMatch.Text =
                [
                    new DocumentLine(
                        outputText,
                        line!.LineNumber,
                        line.PageNumber,
                        line.Words.ToList(),
                        line.Top,
                        line.TopRounded,
                        line.Left,
                        line.LeftRounded)
                ];
                
                lineMatch.MatchType = MatchType.Between;
                lineMatch.MatchedLabel = request.label;
                RemoveRemoves(lineMatch, removedLines);
                
                returnListTop.Add(lineMatch);
            }
        }

        return returnListTop;
    }
}