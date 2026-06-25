using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class ApplicableToMost
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        if (request.label!.Position is LabelPosition.TextToFindIsBetweenLabels
            or LabelPosition.SplitAtLabel
            or LabelPosition.RelatedCategoryPosition)
        {
            return [];
        }

        if (request.label.SkipLineNumbers.Contains(request.line!.LineNumber))
        {
            return [];
        }
        
        if (request.textBeforeAtAndAfterLabel?.Any() != true
            && request.line?.Text.Equals(request.label.Text?.FirstOrDefault()?.Text, StringComparison.InvariantCultureIgnoreCase) == true)
        {
            request.textBeforeAtAndAfterLabel = [
                new()
                {
                    ColumnsText = [request.label.Text?.FirstOrDefault()?.Text!],
                    Label = request.label
                }
            ]!;
        }
        
        if (!LabelMatchingHelper.PotentialMatchOnLabelLine(request.textBeforeAtAndAfterLabel!))
        {
            return [];
        }

        var returnListTop = new List<LabelGroupResult>();
        var textBeforeAtAndAfterLabel = request.textBeforeAtAndAfterLabel!.ToList();

        if (request.label.Position is LabelPosition.LabelIsBeforeTextToFind
            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore)
        {
            textBeforeAtAndAfterLabel.Reverse();
        }
        
        var isMultiple = request.label?.MultipleMatchBehaviour is
            MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel
                or MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel;
        
        foreach (var item in textBeforeAtAndAfterLabel)
        {
            var matchedLabel = item.Label!;
            var text = item.ColumnsText![0];
            
            var labelGroupResult = request.labelGroupResult;
            labelGroupResult.MatchedPosition = MatchedPosition.FullyOnSameLine;
            labelGroupResult.MatchedLabel = matchedLabel;
            
            var t = matchedLabel.IncludeStartLabelText ? request.line!.Text : text;
            var labelText = matchedLabel.Text?.FirstOrDefault()?.Text;

            var columnTextOnly = matchedLabel.Name == "CompanyName"; // TODO make it a flag in config
            
            if (columnTextOnly && labelText != null)
            {
                var column = request.line!.Columns
                    .FirstOrDefault(c =>
                        c.Text.Contains(labelText, StringComparison.InvariantCultureIgnoreCase));

                if (column != null)
                {
                    t = column.Text[(column.Text.IndexOf(labelText, StringComparison.Ordinal) + labelText.Length)..]
                        .Trim();
                }
            }
            
            var over2Lines = false;
            var outputText = DataHelper.RemoveExcludes(
                matchedLabel,
                t,
                true,
                false,
                null,
                out var removedLines);
            
            if (string.IsNullOrEmpty(outputText) || DataHelper.IsCorruptedLine(outputText, request.isOcr))
            {
                continue;
            }

            var lineWords = request.line!.Columns
                .SelectMany(c => c.Words)
                .Select((w, idx) =>
                {
                    w.Text = DataHelper.RemoveExcludes(
                        matchedLabel,
                        w.Text,
                        idx == 0,
                        false,
                        idx,
                        out _);

                    return w;
                })
                .ToList();

            lineWords = DocumentLineColumn.FilterWordsFromText(lineWords, outputText);
            
            var documentLine = request.line.Clone();
            documentLine.Columns.Clear();
            documentLine.Columns.Add(new DocumentLineColumn(lineWords));
            
            if (request.isDateLookup)
            {
                // TODO can swap this out now for shared method in Base
                
                if (Date.AnyIsDate([documentLine], out var matchedLines))
                {
                    matchedLines = RestrictToPossibilities(request.label?.Possibilities, matchedLines);
                    
                    foreach (var matchedLine in matchedLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        
                        FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                        labelGroupResult = CheckContains(request.label, labelGroupResult);
                        
                        if (labelGroupResult == null)
                        {
                            return [];
                        }
                        
                        return await ProcessSubLabelsAsync(request, labelGroupResult);
                    }
                }
                
                continue;
            }
            
            if (request.isDateOrPurposeLookup)
            {
                // TODO can swap this out now for shared method in Base
                
                if (DateOrPurpose.AnyIsDateOrPurpose([documentLine], out var matchedLines))
                {
                    matchedLines = RestrictToPossibilities(request.label?.Possibilities, matchedLines);
                    
                    foreach (var matchedLine in matchedLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        
                        FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                        labelGroupResult = CheckContains(request.label, labelGroupResult);
                        
                        if (labelGroupResult == null)
                        {
                            return [];
                        }
                        
                        return await ProcessSubLabelsAsync(request, labelGroupResult);
                    }
                }
                
                continue;
            }
            
            if (request.isCompanyType
                && !string.IsNullOrEmpty(outputText)
                && (char.IsLower(outputText[0])
                    || outputText.StartsWith("trading as", StringComparison.InvariantCultureIgnoreCase)))
            {
                over2Lines = true;
                outputText = $"{request.previousLines!.FirstOrDefault()?.Text} {outputText}";
            }

            if (request.isNumberLookup)
            {
                // TODO can swap this out now for shared method in Base
                
                if (Number.AnyIsNumber([documentLine], request.label, request.isOcr, out var numberLines))
                {
                    numberLines = RestrictToPossibilities(request.label?.Possibilities, numberLines);

                    if (numberLines.Count > 0)
                    {
                        labelGroupResult = labelGroupResult.Clone(numberLines.Take(1));

                        FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                        labelGroupResult = CheckContains(request.label, labelGroupResult);
                        
                        if (labelGroupResult == null)
                        {
                            return [];
                        }
                        
                        return await ProcessSubLabelsAsync(request, labelGroupResult);
                    }
                }
                
                continue;
            }

            if (request.isLicenceNumberLookup)
            {
                // TODO can swap this out now for shared method in Base

                var isLast = textBeforeAtAndAfterLabel.Last() == item;
                var isTableLine = request.line.Columns.Count >= 5 && !request.line.Text.Any(char.IsLetter);

                var (anyIsLicenceNumber, licenceNumberLines) =
                    LicenceNumber.AnyIsLicenceNumber(
                        [documentLine],
                        request.label!,
                        request.isOcr,
                        request.additionalInformationStore);
                
                if (!isTableLine && anyIsLicenceNumber)
                {
                    licenceNumberLines = RestrictToPossibilities(request.label?.Possibilities, licenceNumberLines);
                    var returnList = new List<LabelGroupResult>();
                    
                    // If its a floating number, its usually some weird internal refernece number
                    if (licenceNumberLines.Count == 1
                        && licenceNumberLines[0].Text == request.line.Text
                        && string.IsNullOrEmpty(request.previousLines?.FirstOrDefault()?.Text)
                        && string.IsNullOrEmpty(request.nextLines?.FirstOrDefault()?.Text))
                    {
                        licenceNumberLines = [];
                    }
                    
                    // If its a number then 'M', its usually some weird internal refernece number
                    if (licenceNumberLines.Count == 1
                        && request.line.Text == $"{licenceNumberLines[0].Text} M"
                        && string.IsNullOrEmpty(request.nextLines?.FirstOrDefault()?.Text))
                    {
                        licenceNumberLines = [];
                    }
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        returnList.AddRange(await ProcessSubLabelsAsync(request, labelGroupResult));
                    }
                    
                    if (!isMultiple)
                    {
                        return CheckContains(request.label, returnList);
                    }

                    returnListTop.AddRange(returnList);
                }
                
                if (isLast)
                {
                    return CheckContains(request.label, returnListTop);
                }
                
                continue;
            }
            
            if (request.label?.Format == LicenceNumberFilename.Constant)
            {
                // TODO can swap this out now for shared method in Base
                
                var (anyIsLicenceNumberF, licenceNumberLinesF) =
                    LicenceNumber.AnyIsLicenceNumber(
                        [documentLine],
                        request.label!,
                        request.isOcr,
                        request.additionalInformationStore);

                if (!anyIsLicenceNumberF)
                {
                    continue;
                }

                licenceNumberLinesF = RestrictToPossibilities(request.label?.Possibilities, licenceNumberLinesF);
                var returnList = new List<LabelGroupResult>();
                    
                foreach (var licenceNumberLine in licenceNumberLinesF)
                {
                    if (!FormattingHelper.GetDmsFileData(
                            licenceNumberLine.Text,
                            request.regionCode,
                            request.licenceNumberMapping,
                            out var dmsFileData))
                    {
                        continue;
                    }

                    var coords = documentLine
                        .Columns
                        .First()
                        .Words
                        .First()
                        .Coordinates;
                        
                    licenceNumberLine.Columns[0].Words.Clear();
                    licenceNumberLine.Columns[0].Words.AddRange(
                        DocumentLineColumn.TextToWords(dmsFileData!.DestinationFileName!, null, coords));
                    labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        
                    returnList.AddRange(await ProcessSubLabelsAsync(request, labelGroupResult));
                }
                    
                return CheckContains(request.label, returnList);
            }

            if ((request.isSingleWord || request.actsLikeSingleWord) && !string.IsNullOrEmpty(t))
            {
                var coords = request
                    .line
                    .Columns
                    .First()
                    .Words
                    .First()
                    .Coordinates;
                
                documentLine.Columns[0].Words.Clear();
                documentLine.Columns[0].Words.Add(new DocumentLineWord(
                    request.isSingleWord ? t.Split(' ')[0] : t,
                    null,
                    coords,
                    null));
                
                labelGroupResult.Clone([documentLine]);
                
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                labelGroupResult = CheckContains(request.label, labelGroupResult);
                if (labelGroupResult == null)
                {
                    return [];
                }
                
                return await ProcessSubLabelsAsync(request, labelGroupResult);
            }

            var isPossiblity = false;
            
            if (matchedLabel.Possibilities?.Any() == true)
            {
                var words = documentLine.Columns.SelectMany(c => c.Words).ToList();
                
                var autoCorrectedOutput = request.isOcr
                    ? AutoCorrectHelper.AutoCorrectText(
                        words,
                        false,
                        request.label?.AutoCorrect ?? false)
                    : words;

                foreach (var possibility in matchedLabel.Possibilities)
                {
                    if (!outputText.Contains(possibility, StringComparison.InvariantCultureIgnoreCase)
                        && !autoCorrectedOutput.Any(aco => aco.Text.Equals(possibility, StringComparison.InvariantCultureIgnoreCase)))
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
                if (isPossiblity)
                {
                    var dLineWords = documentLine.Columns
                        .SelectMany(c => c.Words)
                        .ToList();
                 
                    dLineWords = DocumentLineColumn.FilterWordsFromText(dLineWords, outputText);
                    
                    documentLine.Columns.Clear();
                    documentLine.Columns.Add(new DocumentLineColumn(dLineWords));
                
                    labelGroupResult.Text = [documentLine];
                    labelGroupResult.MatchedPosition = MatchedPosition.OnSameLineSingleWord;
                
                    FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.MatchedLabel.Possibilities = [outputText];

                    labelGroupResult = CheckContains(request.label, labelGroupResult);
                    if (labelGroupResult == null)
                    {
                        return [];
                    }
                    
                    return await ProcessSubLabelsAsync(request, labelGroupResult);
                }
                
                // TODO can swap this out now for shared method in Base
                
                var r = Units.GetMatchesToPossibilities(
                    request.label!,
                    [documentLine],
                    false,
                    labelGroupResult);

                if (r.Count == 0)
                {
                    continue;
                }

                labelGroupResult = labelGroupResult.Clone([documentLine]);
                return CheckContains(request.label, r);
            }
            
            if (!request.label!.DoNotTrimLines)
            {
                outputText = FormattingHelper.TrimFormatting(outputText, true, true);    
            }

            var previousLine = request.previousLines!.FirstOrDefault();
            
            var inputWords = over2Lines && previousLine != null
                ? new List<DocumentLine> { previousLine, documentLine }
                    .SelectMany(dl => dl.Columns)
                    .SelectMany(c => c.Words)
                    .ToList()
                : documentLine.Columns
                    .SelectMany(c => c.Words)
                    .ToList();
            
            var tWords = DocumentLineColumn.FilterWordsFromText(
                inputWords,
                outputText!);

            if (request.isOcr)
            {
                tWords = AutoCorrectHelper.AutoCorrectText(
                    tWords,
                    request.isCompanyType,
                    request.label.AutoCorrect);
            }

            outputText = string.Join(' ', tWords.Select(tw => tw.Text));
            
            if (request.isCompanyType
                && CompanyName.TryGetCompanyOrPersonalName(
                    outputText,
                    matchedLabel,
                    request.lookupConfiguration,
                    out _))
            {
                if (request.label?.Position == LabelPosition.LabelIsInMiddleOfTextToFind)
                {
                    continue;
                }
                
                // Need to look at the next lines also
                /*if (request.label?.Position != LabelPosition.ApplicableToMost
                    && request.nextLines?.Count > 0
                    && CompanyName.TryGetCompanyOrPersonalName(docLine.Clone(request.nextLines[0].Text), matchedLabel, out _))
                {
                    continue;
                }*/
                
                var matchType = over2Lines ?
                    MatchedPosition.PartiallyOnSameLine
                    : MatchedPosition.FullyOnSameLine;

                var coords = documentLine
                    .Columns
                    .First()
                    .Words
                    .First()
                    .Coordinates;
                
                documentLine.Columns[0].Words.Clear();
                documentLine.Columns[0].Words.AddRange(DocumentLineColumn.TextToWords(
                    outputText!,
                    null,
                    coords));
                
                labelGroupResult.Text = [documentLine];
                labelGroupResult.MatchedPosition = matchType;
                
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);

                if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                {
                    labelGroupResult.MatchedLabel.Possibilities = [outputText!];   
                }
                
                labelGroupResult = CheckContains(request.label, labelGroupResult);
                if (labelGroupResult == null)
                {
                    return [];
                }
                
                return await ProcessSubLabelsAsync(request, labelGroupResult);
            }
            
            var trimmedWords = outputText!.Trim().Split(' ');

            if (trimmedWords.Length == 1
                && !string.IsNullOrEmpty(trimmedWords[0])
                && request.isCompanyType)
            {
                var coords = documentLine
                    .Columns
                    .First()
                    .Words
                    .First()
                    .Coordinates;
                
                documentLine.Columns[0].Words.Clear();
                documentLine.Columns[0].Words.Add(new DocumentLineWord(outputText, null, coords, null));
                
                labelGroupResult.Text = [documentLine];
                labelGroupResult.MatchedPosition = MatchedPosition.OnSameLineSingleWord;
                
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                
                if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                {
                    labelGroupResult.MatchedLabel.Possibilities = [outputText];   
                }

                labelGroupResult = CheckContains(request.label, labelGroupResult);
                if (labelGroupResult == null)
                {
                    return [];
                }
                
                return await ProcessSubLabelsAsync(request, labelGroupResult);
            }

            if (!string.IsNullOrWhiteSpace(outputText))
            {
                if (request.label?.Text?.FirstOrDefault()?.Text == null)
                {
                    var coords = documentLine
                        .Columns
                        .First()
                        .Words
                        .First()
                        .Coordinates;
                    
                    documentLine.Columns[0].Words.Clear();
                    documentLine.Columns[0].Words.AddRange(
                        DocumentLineColumn.TextToWords(outputText, null, coords));
                    
                    var lineMatch = labelGroupResult.Clone([documentLine]);
                    lineMatch.MatchedPosition = MatchedPosition.BetweenLabels;
                    
                    FormattingHelper.RemoveRemoves(lineMatch, removedLines);

                    returnListTop.AddRange(await ProcessSubLabelsAsync(request, lineMatch));
                }
                else if (request.label?.Format == Text.Constant)
                {
                    var coords = documentLine
                        .Columns
                        .First()
                        .Words
                        .First()
                        .Coordinates;
                    
                    documentLine.Columns[0].Words.Clear();
                    documentLine.Columns[0].Words.AddRange(
                        DocumentLineColumn.TextToWords(outputText, null, coords));
                    
                    var lineMatch = labelGroupResult.Clone([documentLine]);
                    returnListTop.AddRange(await ProcessSubLabelsAsync(request, lineMatch));
                }
            }
        }
        
        return CheckContains(request.label, returnListTop);
    }

    private static List<LabelGroupResult> CheckContains(LabelToMatch? label, List<LabelGroupResult> results)
    {
        if (label?.MustContain == null || label.MustContain.Count == 0)
        {
            return results;
        }
        
        var returnList = new List<LabelGroupResult>();
        
        foreach (var result in results)
        {
            var matches = label.MustContain.Any(containsInstance =>
                !string.IsNullOrEmpty(containsInstance)
                && result.Text?.Any(t =>
                    t.Text.Contains(containsInstance, StringComparison.InvariantCultureIgnoreCase)) == true);

            if (matches)
            {
                returnList.Add(result);
            }
        }

        return returnList;
    }
    
    private static LabelGroupResult? CheckContains(LabelToMatch? label, LabelGroupResult? result)
    {
        if (label?.MustContain == null || label.MustContain.Count == 0 || result == null)
        {
            return result;
        }
        
        var matches = label.MustContain.Any(containsInstance =>
            !string.IsNullOrEmpty(containsInstance)
            && result.Text?.Any(t =>
                t.Text.Contains(containsInstance, StringComparison.InvariantCultureIgnoreCase)) == true);

        return matches ? result : null;
    }
}