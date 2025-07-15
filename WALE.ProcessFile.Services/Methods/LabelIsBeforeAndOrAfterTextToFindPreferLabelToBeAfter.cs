using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        var labelGroupResult = request.labelGroupResult.Clone();
        labelGroupResult.MatchType = MatchType.NearPreviousLineIsCompany;
        labelGroupResult.MatchedLabel = request.label.Clone();
        labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
        
        var inputLines = request.previousLines.ToList();
        inputLines.Reverse();
        inputLines.AddRange(request.nextLines);
        
        var modifiedLines = RemoveExcludes(request.label, inputLines, out var removedLines);
        
        if (request.isDateOrPurposeLookup && AnyIsDateOrPurpose(request.previousLines, out var matchedLines))
        {
            var returnList = new List<LabelGroupResult>();
                
            foreach (var matchedLine in matchedLines)
            {
                labelGroupResult = labelGroupResult.Clone();
                labelGroupResult.Text = [matchedLine];
                labelGroupResult.MatchedLabel!.Format = "DateOrPurpose";
                RemoveRemoves(labelGroupResult, removedLines);                            
                
                returnList.Add(labelGroupResult);
            }

            return Task.FromResult(returnList);
        }
        
        if (request.isCompanyType && AnyIsCompanyOrPersonalName(modifiedLines, false, request.isOcr, out var companyNameLines))
        {
            labelGroupResult.Text = companyNameLines;
            labelGroupResult.MatchedLabel.Format = "CompanyName";
            RemoveRemoves(labelGroupResult, removedLines);
            
            labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }

        if (request.isNumberLookup && AnyIsNumber(modifiedLines, out var numberLine))
        {
            labelGroupResult.Text = [numberLine!];
            labelGroupResult.MatchedLabel.Format = "Number";
            RemoveRemoves(labelGroupResult, removedLines);                        
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }
        
        if (request.isLicenceNumberLookup && AnyIsLicenceNumber(modifiedLines, request.label, out var licenceNumberLines))
        {
            var returnList = new List<LabelGroupResult>();
                
            foreach (var licenceNumberLine in licenceNumberLines)
            {
                labelGroupResult = labelGroupResult.Clone();
                labelGroupResult.Text = [licenceNumberLine];
                labelGroupResult.MatchedLabel!.Format = "LicenceNumber";
                RemoveRemoves(labelGroupResult, removedLines);
                
                returnList.Add(labelGroupResult);
            }

            return Task.FromResult(returnList);
        }
        
        if (request.isUnitsLookup)
        {
            foreach (var previousLine in modifiedLines)
            {
                if (labelGroupResult.MatchedLabel.Possibilities == null)
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
                        previousLine.Words.ToList())
                    ];
                    labelGroupResult.MatchedLabel.Format = "Units";
                    RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.MatchedLabel.Possibilities = [possibility];
                    
                    return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
                }
            }
        }
        
        return Task.FromResult(new List<LabelGroupResult>());
    }
}