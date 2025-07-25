using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsBeforeTextToFind
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);
        
        var labelGroupResult = request.labelGroupResult.Clone(
            MatchType.NearNextLineIsCompany,
            LabelPosition.LabelIsBeforeTextToFind,
            request.label);

        var modifiedNextLines = DataHelper.RemoveExcludes(
            request.label,
            request.nextLines,
            out var removedLines);
        
        if (request.isDateOrPurposeLookup && DateOrPurpose.AnyIsDateOrPurpose(request.nextLines!, out var matchedLines))
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
            && CompanyName.AnyIsCompanyOrPersonalName(modifiedNextLines, request.label, false, request.isOcr, out var companyNameLine))
        {
            labelGroupResult.Text = companyNameLine;
            labelGroupResult.MatchedLabel.Format = "CompanyName";
            FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }
        
        if (request.isNumberLookup && Number.AnyIsNumber(modifiedNextLines, out var numberLines))
        {
            labelGroupResult.Text = [numberLines.First()];
            labelGroupResult.MatchedLabel.Format = "Number";
            FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
            
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }

        if (request.isLicenceNumberLookup && LicenceNumber.AnyIsLicenceNumber(modifiedNextLines, request.label, out var licenceNumberLines))
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
        
        if (request.isSingleWord && modifiedNextLines.FirstOrDefault() != null)
        {
            var modifiedLine = modifiedNextLines.First();
            
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
                    modifiedLine.LeftRounded)
            ];
            
            labelGroupResult.MatchedLabel.Format = "SingleWord";
            FormattingHelper.RemoveRemoves(labelGroupResult, removedLines);
                
            return Task.FromResult(new List<LabelGroupResult> { labelGroupResult });
        }
        
        if (request.isUnitsLookup)
        {
            foreach (var nextLine in modifiedNextLines)
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