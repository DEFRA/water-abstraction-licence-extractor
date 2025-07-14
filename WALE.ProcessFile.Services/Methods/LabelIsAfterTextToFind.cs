using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsAfterTextToFind
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        var labelGroupResult = request.labelGroupResult.Clone();
        labelGroupResult.MatchType = MatchType.NearPreviousLineIsCompany;
        labelGroupResult.MatchedLabel = label.Clone();
        labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
        
        var modifiedPreviousLines = RemoveExcludes(label, previousLines, out var removedLines);
        
        if (isDateOrPurposeLookup && AnyIsDateOrPurpose(previousLines, out var matchedLines))
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

            return returnList;
        }
        
        if (isCompanyType && AnyIsCompanyOrPersonalName(modifiedPreviousLines, true, isOcr, out var companyNameLine))
        {
            labelGroupResult.Text = companyNameLine;
            labelGroupResult.MatchedLabel.Format = "CompanyName";
            RemoveRemoves(labelGroupResult, removedLines);
            
            return [labelGroupResult];
        }

        if (isNumberLookup && AnyIsNumber(modifiedPreviousLines, out var numberLine))
        {
            labelGroupResult.Text = new[] { numberLine! };
            labelGroupResult.MatchedLabel.Format = "Number";
            RemoveRemoves(labelGroupResult, removedLines);                        
            
            return [labelGroupResult];
        }
        
        if (isLicenceNumberLookup && AnyIsLicenceNumber(modifiedPreviousLines, label, out var licenceNumberLines))
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

            return returnList;
        }
        
        if (isUnitsLookup)
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

                    labelGroupResult.Text = new[] { new DocumentLine(
                        possibility,
                        previousLine.LineNumber,
                        previousLine.PageNumber,
                        previousLine.Words.ToList()) };
                    labelGroupResult.MatchedLabel.Format = "Units";
                    RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.MatchedLabel.Possibilities = [possibility];
                    
                    return [labelGroupResult];
                }
            }
        }
        
        return [];
    }
}