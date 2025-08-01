using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Methods;

public static class BaseMethod
{
    public static List<LabelGroupResult> FilterIntoFormat(
        FunctionInputModel request,
        LabelGroupResult labelGroupResult,
        List<DocumentLine> lines,
        bool isPrevious)
    {
        if (request.label == null)
        {
            throw new ArgumentNullException(nameof(request.label));
        }
        
        var returnList = new List<LabelGroupResult>();

        switch (request.label.Format)
        {
            case DateOrPurpose.Constant:
                if (DateOrPurpose.AnyIsDateOrPurpose(lines, out var matchedLines))
                {
                    foreach (var matchedLine in matchedLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                break;
            case CompanyName.Constant:
                if (CompanyName.AnyIsCompanyOrPersonalName(lines, request.label, isPrevious, request.isOcr,
                    out var companyNameLine))
                {
                    labelGroupResult.Text = companyNameLine;
                    returnList.Add(labelGroupResult);
                }
                
                break;
            case Number.Constant:
                if (Number.AnyIsNumber(lines, out var numberLines))
                {
                    labelGroupResult.Text = [numberLines.First()];
                    returnList.Add(labelGroupResult);
                }
                
                break;
            case LicenceNumber.Constant:
                if (LicenceNumber.AnyIsLicenceNumber(lines, request.label, out var licenceNumberLines))
                {
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case Units.Constant:
                returnList.AddRange( Units.GetMatchesToPossibilities(request.label, lines, labelGroupResult));
                break;
            case SingleWord.Constant:
                returnList.AddRange(SingleWord.FindSingleWord(lines, labelGroupResult));
                break;
            case ActsLikeSingleWord.Constant:
                returnList.AddRange(ActsLikeSingleWord.FindSingleWord(lines, labelGroupResult));
                break;
            case "Text":
                returnList.Add(labelGroupResult); // TODO should probably filter in some way
                break;
        }

        return returnList;
    }
    
    public static async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        FunctionInputModel request,
        LabelGroupResult labelGroupResult)
    {
        var results = new List<LabelGroupResult>
        {
            labelGroupResult
        };

        return await ProcessSubLabelsAsync(request, results);
    }
    
    public static async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        FunctionInputModel request,
        List<LabelGroupResult> results)
    {
        foreach (var result in results)
        {
            var subResults = await request.pdfDataExtractorService!.ProcessSubLabelsAsync(
                request.label!,
                result.Text!,
                request.isOcr,
                request.serviceName,
                request.labelGroupName!,
                request.licenceMapping!,
                request.previouslyParsedPaths!,
                request.outputFolder!,
                request.useCache);
        
            if (request.label!.MinimumSubMatches.HasValue
                && request.label.MinimumSubMatches.Value > subResults.Count)
            {
                return [];
            }

            result.SubResults = subResults;
        }

        return results;
    }
}