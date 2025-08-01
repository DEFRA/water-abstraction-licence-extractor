using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Methods;

public static class LabelIsAfterTextToFind
{
    public static Task<List<LabelGroupResult>> FunctionAsync(FunctionInputModel request)
    {
        ArgumentNullException.ThrowIfNull(request.labelGroupResult);
        ArgumentNullException.ThrowIfNull(request.label);

        var labelGroupResult = request.labelGroupResult.Clone(
            MatchType.NearPreviousLineIsCompany,
            LabelPosition.LabelIsAfterTextToFind,
            request.label);
        
        var modifiedPreviousLines = DataHelper.RemoveExcludes(
            request.label,
            request.previousLines,
            out var removedLines);

        var returnList = new List<LabelGroupResult>();

        switch (request.label.Format)
        {
            case DateOrPurpose.Constant:
                if (DateOrPurpose.AnyIsDateOrPurpose(modifiedPreviousLines, out var matchedLines))
                {
                    foreach (var matchedLine in matchedLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                break;
            case CompanyName.Constant:
                if (CompanyName.AnyIsCompanyOrPersonalName(modifiedPreviousLines, request.label, true, request.isOcr,
                    out var companyNameLine))
                {
                    labelGroupResult.Text = companyNameLine;
                    returnList.Add(labelGroupResult);
                }
                
                break;
            case Number.Constant:
                if (Number.AnyIsNumber(modifiedPreviousLines, out var numberLines))
                {
                    labelGroupResult.Text = [numberLines.First()];
                    returnList.Add(labelGroupResult);
                }
                
                break;
            case LicenceNumber.Constant:
                if (LicenceNumber.AnyIsLicenceNumber(modifiedPreviousLines, request.label, out var licenceNumberLines))
                {
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case Units.Constant:
                returnList.AddRange( Units.GetMatchesToPossibilities(request.label, modifiedPreviousLines, labelGroupResult));
                break;
            case SingleWord.Constant:
                returnList.AddRange(SingleWord.FindSingleWord(modifiedPreviousLines, labelGroupResult));
                break;
            case ActsLikeSingleWord.Constant:
                returnList.AddRange(ActsLikeSingleWord.FindSingleWord(modifiedPreviousLines, labelGroupResult));
                break;
            case "Text":
                throw new NotImplementedException();
        }

        foreach (var item in returnList)
        {
            FormattingHelper.RemoveRemoves(item, removedLines);
        }
        
        return Task.FromResult(returnList);
    }
}