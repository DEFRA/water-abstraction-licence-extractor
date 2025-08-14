using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;
using static WALE.ProcessFile.Services.Methods.BaseMethod;

namespace WALE.ProcessFile.Services.Methods;

public static class ApplicableToMost
{
    public static async Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var returnListTop = new List<LabelGroupResult>();
        
        if (request.label!.Position is LabelPosition.TextToFindIsBetweenLabels
            or LabelPosition.Split
            or LabelPosition.RelatedCategoryPosition)
        {
            return returnListTop;
        }
        
        if (!LabelMatchingHelper.PotentialMatchOnLabelLine(request.textBeforeAndAfterLabel!))
        {
            return returnListTop;
        }
        
        foreach (var (text, matchedLabel) in request.textBeforeAndAfterLabel!)
        {
            var labelGroupResult = request.labelGroupResult;
            labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
            labelGroupResult.MatchedLabel = matchedLabel;
            
            var t = matchedLabel.IncludeLabelText ? request.line!.Text : text;
            
            var over2Lines = false;
            var outputText = DataHelper.RemoveExcludes(matchedLabel, t!, true, out var removedLines);

            if (DataHelper.IsCorruptedText(outputText))
            {
                continue;
            }
            
            var documentLine = request.line!.Clone();
            documentLine.Columns.Clear();
            documentLine.Columns.Add(new DocumentLineColumn(outputText));
            
            if (request.isDateOrPurposeLookup)
            {
                // TODO can swap this out now for shared method in Base
                
                if (DateOrPurpose.AnyIsDateOrPurpose([documentLine], out var matchedLines))
                {
                    matchedLines = RestrictToPossibilities(request, matchedLines);
                    
                    foreach (var matchedLine in matchedLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        
                        FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                        return await ProcessSubLabelsAsync(request, labelGroupResult);                        
                    }
                }
                
                continue;
            }
            
            if (request.isCompanyType
                && (char.IsLower(outputText[0])
                    || outputText.StartsWith("trading as", StringComparison.InvariantCultureIgnoreCase)))
            {
                over2Lines = true;
                outputText = $"{request.previousLines!.FirstOrDefault()?.Text} {outputText}";
            }

            if (request.isNumberLookup)
            {
                // TODO can swap this out now for shared method in Base
                
                if (Number.AnyIsNumber([documentLine], out var numberLines))
                {
                    numberLines = RestrictToPossibilities(request, numberLines);

                    if (numberLines.Count > 0)
                    {
                        labelGroupResult = labelGroupResult.Clone(numberLines.Take(1));

                        FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                        return await ProcessSubLabelsAsync(request, labelGroupResult);
                    }
                }
                
                continue;
            }

            if (request.isLicenceNumberLookup)
            {
                // TODO can swap this out now for shared method in Base
                
                if (LicenceNumber.AnyIsLicenceNumber([documentLine], request.label!, out var licenceNumberLines))
                {
                    licenceNumberLines = RestrictToPossibilities(request, licenceNumberLines);
                    var returnList = new List<LabelGroupResult>();
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        returnList.AddRange(await ProcessSubLabelsAsync(request, labelGroupResult));
                    }

                    return returnList;
                }
                
                continue;
            }
            
            if (request.label?.Format == LicenceNumberFilename.Constant)
            {
                // TODO can swap this out now for shared method in Base
                
                if (LicenceNumber.AnyIsLicenceNumber([documentLine], request.label!, out var licenceNumberLines))
                {
                    licenceNumberLines = RestrictToPossibilities(request, licenceNumberLines);
                    var returnList = new List<LabelGroupResult>();
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        if (request.licenceMapping?.TryGetValue(licenceNumberLine.Text, out var relatedFileName) != true)
                        {
                            continue;
                        }

                        licenceNumberLine.Columns[0].Text = relatedFileName!;
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        
                        returnList.AddRange(await ProcessSubLabelsAsync(request, labelGroupResult));
                    }

                    return returnList;
                }
                
                continue;
            }

            if ((request.isSingleWord || request.actsLikeSingleWord) && !string.IsNullOrEmpty(t))
            {
                documentLine.Columns[0].Text = request.isSingleWord ? t.Split(' ')[0] : t;
                labelGroupResult.Clone([documentLine]);
                
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                return await ProcessSubLabelsAsync(request, labelGroupResult);
            }

            var isPossiblity = false;
            
            if (matchedLabel.Possibilities?.Any() == true)
            {
                var autoCorrectedOutputText = AutoCorrectHelper.AutoCorrectText(
                    documentLine.Text,false);
                
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
                if (isPossiblity)
                {
                    documentLine.Columns.Clear();
                    documentLine.Columns.Add(new DocumentLineColumn(outputText));
                
                    labelGroupResult.Text = [documentLine];
                    labelGroupResult.MatchType = MatchType.SameLineSingleWord;
                
                    FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.MatchedLabel.Possibilities = [outputText];
                
                    return await ProcessSubLabelsAsync(request, labelGroupResult);
                }
                
                // TODO can swap this out now for shared method in Base
                
                var r = Units.GetMatchesToPossibilities(
                    request.label!,
                    [documentLine],
                    labelGroupResult);

                if (r.Count == 0)
                {
                    continue;
                }

                labelGroupResult = labelGroupResult.Clone([documentLine]);
                return r;
            }

            outputText = FormattingHelper.TrimFormatting(outputText, true);
            outputText = request.isOcr
                ? AutoCorrectHelper.AutoCorrectText(outputText!, request.isCompanyType)
                : outputText;

            if (request.isCompanyType
                && CompanyName.TryGetCompanyOrPersonalName(outputText, matchedLabel, out _))
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
                    MatchType.SameLineIsCompany2Lines
                    : MatchType.SameLineIsCompany1Line;

                documentLine.Columns[0].Text = outputText!;
                
                labelGroupResult.Text = [documentLine];
                labelGroupResult.MatchType = matchType;
                
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);

                if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                {
                    labelGroupResult.MatchedLabel.Possibilities = [outputText!];   
                }
                
                return await ProcessSubLabelsAsync(request, labelGroupResult);
            }

            var trimmedSplit = outputText!.Trim().Split(' ');

            if (trimmedSplit.Length == 1
                && !string.IsNullOrEmpty(trimmedSplit[0])
                && request.isCompanyType)
            {
                documentLine.Columns[0].Text = outputText;
                
                labelGroupResult.Text = [documentLine];
                labelGroupResult.MatchType = MatchType.SameLineSingleWord;
                
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                
                if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                {
                    labelGroupResult.MatchedLabel.Possibilities = [outputText];   
                }
                
                return await ProcessSubLabelsAsync(request, labelGroupResult);
            }

            if (!string.IsNullOrWhiteSpace(outputText))
            {
                if (request.label?.Text == null)
                {
                    documentLine.Columns[0].Text = outputText;
                    var lineMatch = labelGroupResult.Clone([documentLine]);
                    lineMatch.MatchType = MatchType.Between;
                    
                    FormattingHelper.RemoveRemoves(lineMatch, removedLines);

                    returnListTop.AddRange(await ProcessSubLabelsAsync(request, lineMatch));
                }
                else if (request.label?.Format == Text.Constant)
                {
                    documentLine.Columns[0].Text = outputText;
                    var lineMatch = labelGroupResult.Clone([documentLine]);

                    returnListTop.AddRange(await ProcessSubLabelsAsync(request, lineMatch));
                }
            }
        }
        
        return returnListTop;
    }
}