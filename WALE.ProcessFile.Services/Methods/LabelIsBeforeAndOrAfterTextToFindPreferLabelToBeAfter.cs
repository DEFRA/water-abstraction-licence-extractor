using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone(
            MatchType.NearPreviousLineIsCompany,
            LabelPosition.LabelIsAfterTextToFind,
            request.label);
        
        var inputLines = request.previousLines!.ToList();
        inputLines.Reverse();
        inputLines.AddRange(request.nextLines!);
        
        var modifiedLines = DataHelper.RemoveExcludes(
            request.label,
            inputLines,
            out var removedLines);
        
        if (request.isDateOrPurposeLookup && DateOrPurpose.AnyIsDateOrPurpose(request.previousLines!, out var matchedLines))
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
            && CompanyName.AnyIsCompanyOrPersonalName(modifiedLines,  request.label, false, request.isOcr, out var companyNameLines))
        {
            labelGroupResult.Text = companyNameLines;
            labelGroupResult.MatchedLabel!.Format = "CompanyName";
            FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
            
            labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }

        if (request.isNumberLookup && Number.AnyIsNumber(modifiedLines, out var numberLines))
        {
            labelGroupResult.Text = [numberLines.First()];
            labelGroupResult.MatchedLabel!.Format = "Number";
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
        
        if (request.isUnitsLookup)
        {
            foreach (var previousLine in modifiedLines)
            {
                if (labelGroupResult.MatchedLabel!.Possibilities == null)
                {
                    continue;
                }
                
                foreach (var possibility in labelGroupResult.MatchedLabel.Possibilities!)
                {
                    if (!previousLine.Text.Contains(possibility,
                            StringComparison.InvariantCultureIgnoreCase))
                    {
                        continue;
                    }

                    labelGroupResult.Text =
                    [
                        new DocumentLine(
                        possibility,
                        previousLine.LineNumber,
                        previousLine.PageNumber,
                        previousLine.Words.ToList(),
                        previousLine.Top,
                        previousLine.TopRounded,
                        previousLine.Left,
                        previousLine.LeftRounded)
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