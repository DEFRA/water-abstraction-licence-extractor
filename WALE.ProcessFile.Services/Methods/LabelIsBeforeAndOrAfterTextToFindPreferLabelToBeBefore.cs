using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        var labelGroupResult = request.labelGroupResult.Clone();
        labelGroupResult.MatchType = MatchType.NearNextLineIsCompany;
        labelGroupResult.MatchedLabel = request.label.Clone();
        labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsBeforeTextToFind;

        var inputLines = request.previousLines.ToList();
        inputLines.AddRange(request.nextLines);
        
        var modifiedLines = RemoveExcludes(request.label, inputLines, out var removedLines);
        
        if (request.isDateOrPurposeLookup && AnyIsDateOrPurpose(inputLines, out var matchedLines))
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
            
            return Task.FromResult([labelGroupResult]);
        }
        
        if (request.isNumberLookup && AnyIsNumber(modifiedLines, out var numberLine))
        {
            labelGroupResult.Text = [numberLine!];
            labelGroupResult.MatchedLabel.Format = "Number";
            RemoveRemoves(labelGroupResult, removedLines);
            
            return Task.FromResult([labelGroupResult]);
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
        
        if (request.isSingleWord && modifiedLines.FirstOrDefault() != null)
        {
            labelGroupResult.Text =
            [
                new DocumentLine(
                modifiedLines.First().Text.Split(' ')[0],
                modifiedLines.First().LineNumber,
                modifiedLines.First().PageNumber,
                modifiedLines.First().Words.ToList())
            ];
            
            labelGroupResult.MatchedLabel.Format = "SingleWord";
            RemoveRemoves(labelGroupResult, removedLines);
                
            return Task.FromResult([labelGroupResult]);
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
                            nextLine.Words.ToList())
                    ];
                    
                    labelGroupResult.MatchedLabel.Format = "Units";
                    RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.MatchedLabel.Possibilities = [possibility];
                    
                    return Task.FromResult([labelGroupResult]);
                }
            }
        }
        
        return Task.FromResult([]);
    }
}