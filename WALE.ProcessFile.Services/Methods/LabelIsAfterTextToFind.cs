using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsAfterTextToFind
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        if (request.labelGroupResult == null)
        {
            throw new ArgumentNullException(nameof(request.labelGroupResult));
        }
        
        if (request.label == null)
        {
            throw new ArgumentNullException(nameof(request.label));
        }
        
        var labelGroupResult = request.labelGroupResult.Clone();
        labelGroupResult.MatchType = MatchType.NearPreviousLineIsCompany;
        labelGroupResult.MatchedLabel = request.label.Clone();
        labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
        
        var modifiedPreviousLines = RemoveExcludes(request.label, request.previousLines, out var removedLines);
        
        if (request.isDateOrPurposeLookup && AnyIsDateOrPurpose(request.previousLines!, out var matchedLines))
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
        
        if (request.isCompanyType && AnyIsCompanyOrPersonalName(modifiedPreviousLines, true, request.isOcr, out var companyNameLine))
        {
            labelGroupResult.Text = companyNameLine;
            labelGroupResult.MatchedLabel.Format = "CompanyName";
            RemoveRemoves(labelGroupResult, removedLines);
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }

        if (request.isNumberLookup && AnyIsNumber(modifiedPreviousLines, out var numberLine))
        {
            labelGroupResult.Text = [numberLine!];
            labelGroupResult.MatchedLabel.Format = "Number";
            RemoveRemoves(labelGroupResult, removedLines);                        
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }
        
        if (request.isLicenceNumberLookup && AnyIsLicenceNumber(modifiedPreviousLines, request.label, out var licenceNumberLines))
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
            foreach (var previousLine in modifiedPreviousLines)
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
        
        return  Task.FromResult(new List<LabelGroupResult>());
    }
}