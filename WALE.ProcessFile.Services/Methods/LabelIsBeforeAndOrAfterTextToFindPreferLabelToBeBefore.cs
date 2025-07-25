using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone();
        labelGroupResult.MatchType = MatchType.NearNextLineIsCompany;
        labelGroupResult.MatchedLabel = request.label.Clone();
        labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsBeforeTextToFind;

        var inputLines = request.previousLines!.ToList();
        inputLines.AddRange(request.nextLines!);
        
        var modifiedLines = DataHelper.RemoveExcludes(request.label, inputLines, out var removedLines);
        
        if (request.isDateOrPurposeLookup && DateOrPurpose.AnyIsDateOrPurpose(inputLines, out var matchedLines))
        {
            var returnList = new List<LabelGroupResult>();
                
            foreach (var matchedLine in matchedLines)
            {
                labelGroupResult = labelGroupResult.Clone();
                labelGroupResult.Text = [matchedLine];
                labelGroupResult.MatchedLabel!.Format = "DateOrPurpose";
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);                            
                
                returnList.Add(labelGroupResult);
            }

            return Task.FromResult(returnList);
        }
        
        if (request.isCompanyType
            && CompanyName.AnyIsCompanyOrPersonalName(modifiedLines, request.label, false, request.isOcr, out var companyNameLines))
        {
            labelGroupResult.Text = companyNameLines;
            labelGroupResult.MatchedLabel.Format = "CompanyName";
            FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);

            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }
        
        if (request.isNumberLookup && Number.AnyIsNumber(modifiedLines, out var numberLines))
        {
            labelGroupResult.Text = [numberLines.First()];
            labelGroupResult.MatchedLabel.Format = "Number";
            FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }

        if (request.isLicenceNumberLookup && LicenceNumber.AnyIsLicenceNumber(modifiedLines, request.label, out var licenceNumberLines))
        {
            var returnList = new List<LabelGroupResult>();
                
            foreach (var licenceNumberLine in licenceNumberLines)
            {
                labelGroupResult = labelGroupResult.Clone();
                labelGroupResult.Text = [licenceNumberLine];
                labelGroupResult.MatchedLabel!.Format = "LicenceNumber";
                FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);                            
                
                returnList.Add(labelGroupResult);
            }

            return Task.FromResult(returnList);
        }
        
        if (request.isSingleWord && modifiedLines.FirstOrDefault() != null)
        {
            var modifiedLine = modifiedLines.First();
            
            labelGroupResult.Text =
            [
                new DocumentLine(
                    modifiedLine.Text.Split(' ')[0],
                    modifiedLine.LineNumber,
                    modifiedLine.PageNumber,
                    modifiedLine.Words.ToList(),
                    modifiedLine.Top,
                    modifiedLine.TopRounded,
                    modifiedLine.Left,
                    modifiedLine.LeftRounded
                )
            ];
            
            labelGroupResult.MatchedLabel.Format = "SingleWord";
            FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });            
        }
        
        if (request.isUnitsLookup)
        {
            foreach (var nextLine in modifiedLines)
            {
                if (labelGroupResult.MatchedLabel.Possibilities == null)
                {
                    continue;
                }
                
                foreach (var possibility in labelGroupResult.MatchedLabel.Possibilities!)
                {
                    if (!nextLine.Text.Contains(possibility,
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    labelGroupResult.Text =
                    [
                        new DocumentLine(
                            possibility,
                            nextLine.LineNumber,
                            nextLine.PageNumber,
                            nextLine.Words.ToList(),
                            nextLine.Top,
                            nextLine.TopRounded,
                            nextLine.Left,
                            nextLine.LeftRounded)
                    ];
                    
                    labelGroupResult.MatchedLabel.Format = "Units";
                    FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.MatchedLabel.Possibilities = [possibility];
                    
                    return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
                }
            }
        }

        return Task.FromResult(new List<LabelGroupResult>());
    }
}