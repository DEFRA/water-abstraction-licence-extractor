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
                    matchedLines = RestrictToPossibilities(request, matchedLines);
                    
                    foreach (var matchedLine in matchedLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case CompanyName.Constant:
                if (CompanyName.AnyIsCompanyOrPersonalName(lines, request.label, isPrevious, request.isOcr,
                    out var companyNameLines))
                {
                    companyNameLines = RestrictToPossibilities(request, companyNameLines!);

                    if (companyNameLines.Count > 0)
                    {
                        labelGroupResult = labelGroupResult.Clone(companyNameLines);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case Number.Constant:
                if (Number.AnyIsNumber(lines, out var numberLines))
                {
                    numberLines = RestrictToPossibilities(request, numberLines);

                    if (numberLines.Count > 0)
                    {
                        labelGroupResult = labelGroupResult.Clone(numberLines.Take(1));
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case LicenceNumber.Constant:
                if (LicenceNumber.AnyIsLicenceNumber(lines, request.label, out var licenceNumberLines))
                {
                    licenceNumberLines = RestrictToPossibilities(request, licenceNumberLines);
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case LicenceNumberFilename.Constant:
                if (LicenceNumber.AnyIsLicenceNumber(lines, request.label, out var licenceNumberLines2))
                {
                    licenceNumberLines = RestrictToPossibilities(request, licenceNumberLines2);
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        if (request.licenceMapping?.TryGetValue(licenceNumberLine.Text, out var relatedFileName) != true)
                        {
                            continue;
                        }
                        
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine.Clone(relatedFileName!)]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case Units.Constant:
                returnList.AddRange( Units.GetMatchesToPossibilities(request.label, lines, labelGroupResult));
                
                break;
            case SingleWord.Constant:
                var results = SingleWord.FindSingleWord(lines, labelGroupResult);
                returnList.AddRange(RestrictToPossibilities(request, results));
                
                break;
            case ActsLikeSingleWord.Constant:
                var matches = ActsLikeSingleWord.FindSingleWord(lines, labelGroupResult);
                returnList.AddRange(RestrictToPossibilities(request, matches));

                break;
            case Text.Constant:
                var result = RestrictToPossibility(request, labelGroupResult);
                if (result != null) returnList.Add(labelGroupResult); // TODO should probably filter in some way

                break;
        }

        return returnList;
    }
    
    public static List<DocumentLine> RestrictToPossibilities(
        FunctionInputModel request,
        IReadOnlyList<DocumentLine> lines)
    {
        if (request.label!.Possibilities?.Any() != true)
        {
            return lines.ToList();
        }

        return lines
            .Where(line => request.label.Possibilities
                .Any(possibility => possibility == line.Text))
            .ToList();
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
    
    private static LabelGroupResult? RestrictToPossibility(
        FunctionInputModel request,
        LabelGroupResult result)
    {
        if (request.label!.Possibilities?.Any() != true)
        {
            return result;
        }

        return request.label.Possibilities.Any(possibility => possibility == result.Text?.FirstOrDefault()?.Text)
            ? result
            : null;
    }

    private static List<LabelGroupResult> RestrictToPossibilities(
        FunctionInputModel request,
        IReadOnlyList<LabelGroupResult> results)
    {
        if (request.label!.Possibilities?.Any() != true)
        {
            return results.ToList();
        }

        return results
            .Where(result => request.label.Possibilities
                .Any(possibility => possibility == result.Text?.FirstOrDefault()?.Text))
            .ToList();
    }
}