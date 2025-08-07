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
        
        if (request.label!.Position is LabelPosition.TextToFindIsBetweenLabels or LabelPosition.Split)
        {
            return returnListTop;
        }
        
        var line = request.line;
        
        if (!LabelMatchingHelper.PotentialMatchOnLabelLine(request.textBeforeAndAfterLabel!))
        {
            return returnListTop;
        }
        
        foreach (var (text, matchedLabel) in request.textBeforeAndAfterLabel!)
        {
            var labelGroupResult = request.labelGroupResult.Clone(matchedLabel);
            labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
            labelGroupResult.MatchedLabel = matchedLabel;
            
            var t = matchedLabel.IncludeLabelText ? request.line!.Text : text;
            
            var over2Lines = false;
            var outputText = DataHelper.RemoveExcludes(matchedLabel, t!, out var removedLines);

            if (DataHelper.IsCorruptedText(outputText))
            {
                continue;
            }

            var docLine = new DocumentLine(
                outputText,
                line!.LineNumber,
                line.PageNumber,
                line.Words,
                line.Bottom,
                line.BottomRounded,
                line.Left);
            
            if (request.isDateOrPurposeLookup)
            {
                // TODO can swap this out now for shared method in Base
                
                if (DateOrPurpose.AnyIsDateOrPurpose([docLine], out var matchedLines))
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
                
                if (Number.AnyIsNumber([docLine], out var numberLines))
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
                
                if (LicenceNumber.AnyIsLicenceNumber([docLine], request.label!, out var licenceNumberLines))
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
                
                if (LicenceNumber.AnyIsLicenceNumber([docLine], request.label!, out var licenceNumberLines))
                {
                    licenceNumberLines = RestrictToPossibilities(request, licenceNumberLines);
                    var returnList = new List<LabelGroupResult>();
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        if (request.licenceMapping?.TryGetValue(licenceNumberLine.Text, out var relatedFileName) != true)
                        {
                            continue;
                        }
                        
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine.Clone(relatedFileName!)]);
                        returnList.AddRange(await ProcessSubLabelsAsync(request, labelGroupResult));
                    }

                    return returnList;
                }
                
                continue;
            }

            if ((request.isSingleWord || request.actsLikeSingleWord) && !string.IsNullOrEmpty(t))
            {
                labelGroupResult.Text = [docLine.Clone(request.isSingleWord ? t.Split(' ')[0] : t)];
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);

                return await ProcessSubLabelsAsync(request, labelGroupResult);
            }

            var isPossiblity = false;
            
            if (matchedLabel.Possibilities?.Any() == true)
            {
                var autoCorrectedOutputText = AutoCorrectHelper.AutoCorrectText(
                    docLine,
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
                // TODO can swap this out now for shared method in Base
                
                var r = Units.GetMatchesToPossibilities(
                    request.label!,
                    [docLine],
                    labelGroupResult);

                if (r.Count == 0)
                {
                    continue;
                }

                labelGroupResult = labelGroupResult.Clone([docLine]);
                
                return r;
            }

            outputText = FormattingHelper.TrimFormatting(outputText);
            outputText = request.isOcr ? AutoCorrectHelper.AutoCorrectText(
                docLine.Clone(outputText!),
                request.isCompanyType) : outputText;

            if (request.isCompanyType
                && CompanyName.TryGetCompanyOrPersonalName(docLine.Clone(outputText!), matchedLabel, out _))
            {
                var matchType = over2Lines ?
                    MatchType.SameLineIsCompany2Lines
                    : MatchType.SameLineIsCompany1Line;
                
                labelGroupResult.Text = [docLine.Clone(outputText!)];
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
                labelGroupResult.Text = [docLine.Clone(outputText)];
                
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
                    var lineMatch = labelGroupResult.Clone();
                    lineMatch.Text = [docLine.Clone(outputText)];

                    lineMatch.MatchType = MatchType.Between;
                    FormattingHelper.RemoveRemoves(lineMatch, removedLines);

                    returnListTop.AddRange(await ProcessSubLabelsAsync(request, lineMatch));
                }
                else if (request.label?.Format == Text.Constant)
                {
                    var lineMatch = labelGroupResult.Clone();
                    lineMatch.Text = [docLine.Clone(outputText)];

                    returnListTop.AddRange(await ProcessSubLabelsAsync(request, lineMatch));
                }
            }
        }
        
        return returnListTop;
    }
}