using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Methods;

public static class BaseMethod
{
    public static List<LabelGroupResult> FilterIntoFormat(
        FunctionInputModel request,
        LabelGroupResult labelGroupResult,
        List<DocumentLine> lines,
        bool lineNumbersAreDescending)
    {
        if (request.label == null)
        {
            throw new ArgumentNullException(nameof(request.label));
        }
        
        var returnList = new List<LabelGroupResult>();

        if (lines.Any(line => LabelMatchingHelper.ShouldSkipBlockAsForbidden(line.Text, request.label)))
        {
            return returnList;
        }
        
        switch (request.label.Format)
        {
            case Date.Constant:
                if (Date.AnyIsDate(lines, out var matchedLinesDates)) // TODO when just want one column, this function should get it
                {
                    matchedLinesDates = RestrictToPossibilities(request.label?.Possibilities, matchedLinesDates);
                    
                    foreach (var matchedLine in matchedLinesDates)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case DateOrPurpose.Constant:
                if (DateOrPurpose.AnyIsDateOrPurpose(lines, out var matchedLines)) // TODO when just want one column, this function should get it
                {
                    matchedLines = RestrictToPossibilities(request.label?.Possibilities, matchedLines);
                    
                    foreach (var matchedLine in matchedLines)
                    {
                        labelGroupResult = labelGroupResult.Clone([matchedLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case CompanyName.Constant:
                if (CompanyName.AnyIsCompanyOrPersonalName(lines, request.label, lineNumbersAreDescending, request.isOcr,
                    out var companyNameLines))
                {
                    companyNameLines = RestrictToPossibilities(request.label?.Possibilities, companyNameLines!);

                    if (companyNameLines.Count > 0)
                    {
                        labelGroupResult = labelGroupResult.Clone(companyNameLines);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case Number.Constant:
                if (Number.AnyIsNumber(lines, request.label, out var numberLines))
                {
                    numberLines = RestrictToPossibilities(request.label?.Possibilities, numberLines);

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
                    licenceNumberLines = RestrictToPossibilities(request.label?.Possibilities, licenceNumberLines);
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        if (LabelMatchingHelper.ShouldSkipLineAsForbidden(licenceNumberLine.Text, request.label!))
                        {
                            continue;
                        }
                        
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case LicenceNumberFilename.Constant:
                if (LicenceNumber.AnyIsLicenceNumber(lines, request.label, out var licenceNumberLines2))
                {
                    licenceNumberLines = RestrictToPossibilities(request.label?.Possibilities, licenceNumberLines2);
                    
                    foreach (var licenceNumberLine in licenceNumberLines)
                    {
                        if (request.licenceMapping?.TryGetValue(licenceNumberLine.Text, out var relatedFileName) != true)
                        {
                            continue;
                        }

                        licenceNumberLine.Columns[0].Text = relatedFileName!;
                        labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);

                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case Units.Constant:
                returnList.AddRange( Units.GetMatchesToPossibilities(request.label, lines, lineNumbersAreDescending, labelGroupResult));
                
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
                if (result?.Text != null) returnList.Add(result);

                break;
        }

        return returnList;
    }
    
    public static List<DocumentLine> RestrictToPossibilities(
        IReadOnlyList<string>? possibilities,
        IReadOnlyList<DocumentLine> lines)
    {
        if (possibilities?.Any() != true)
        {
            return lines.ToList();
        }

        return lines
            .Where(line => possibilities
                .Any(possibility => line.Text.Contains(possibility)))
            .Select(line =>
            {
                var possibility = possibilities
                    .First(possibility => line.Text.Contains(possibility));

                var clonedLine = line.Clone();
                clonedLine.Columns.Clear();
                clonedLine.Columns.Add(new DocumentLineColumn(possibility));

                return clonedLine;
            })
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
                request.outputService!,
                request.cacheService!);
            
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

        var possiblityFound = request.label.Possibilities.Any(possibility =>
            result.Text?.FirstOrDefault()?.Text.Contains(possibility) == true);

        if (possiblityFound)
        {
            var possibility = request.label.Possibilities
                .First(possibility => result.Text!.First().Text.Contains(possibility));

            var clonedLine = result.Text!.First().Clone();
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(possibility));

            var clonedResult = result.Clone();
            clonedResult.Text = [clonedLine];
            
            return clonedResult;
        }

        return null;
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
                .Any(possibility =>
                    result.Text?.FirstOrDefault()?.Text.Contains(possibility) == true))
            .Select(result =>
            {
                var lineText = result.Text!.First().Text;
                
                var possibility = request.label.Possibilities
                    .First(possibility => lineText.Contains(possibility));

                var clonedLine = result.Text!.First().Clone();
                clonedLine.Columns.Clear();
                clonedLine.Columns.Add(new DocumentLineColumn(possibility));
                
                var clonedResult = result.Clone();
                clonedResult.Text = [clonedLine];

                return clonedResult;
            })
            .ToList();
    }
}