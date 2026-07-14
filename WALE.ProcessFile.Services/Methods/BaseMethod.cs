using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
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
        
        if (request.label.SkipLineNumbers.Contains(request.line!.LineNumber))
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
                var (anyFound, companyNameLines) =
                    CompanyName.AnyIsCompanyOrPersonalName(
                        lines,
                        request.label,
                        lineNumbersAreDescending,
                        request.isOcr,
                        request.lookupConfiguration);
                
                if (anyFound)
                {
                    companyNameLines = RestrictToPossibilities(request.label?.Possibilities, companyNameLines);
                    
                    if (companyNameLines.Count > 0)
                    {
                        labelGroupResult = labelGroupResult.Clone(companyNameLines);
                        returnList.Add(labelGroupResult);
                    }
                }
                
                break;
            case Number.Constant:
                if (Number.AnyIsNumber(lines, request.label, request.isOcr, out var numberLines))
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
                {
                    var (success, licenceNumberLines) = LicenceNumber.AnyIsLicenceNumber(
                        lines,
                        request.label,
                        request.isOcr,
                        request.additionalInformationStore);
                    
                    if (success)
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
                }
                
                break;
            case LicenceNumberFilename.Constant:
                {
                    var (success, licenceNumberLinesF) = LicenceNumber.AnyIsLicenceNumber(
                            lines,
                            request.label,
                            request.isOcr,
                            request.additionalInformationStore);
                    
                    if (success)
                    {
                        var licenceNumberLines = RestrictToPossibilities(request.label?.Possibilities, licenceNumberLinesF);

                        foreach (var licenceNumberLine in licenceNumberLines)
                        {
                            if (!FormattingHelper.GetDmsFileData(
                                licenceNumberLine.Text,
                                request.regionCode,
                                request.licenceNumberMapping,
                                out var dmsFileData))
                            {
                                continue;
                            }
                            
                            var coords = licenceNumberLine
                                .Columns
                                .First()
                                .Words
                                .First()
                                .Coordinates;
                            
                            licenceNumberLine.Columns[0].Words.Clear();
                            licenceNumberLine.Columns[0].Words.AddRange(
                                DocumentLineColumn.TextToWords(dmsFileData!.DestinationFileName!, null, coords));
                            
                            labelGroupResult = labelGroupResult.Clone([licenceNumberLine]);

                            returnList.Add(labelGroupResult);
                        }
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

                if (result.HasPossiblites)
                {
                    if (result.LabelGroupResult!.Text != null)
                    {
                        returnList.Add(result.LabelGroupResult);
                    }
                }
                else
                {
                    labelGroupResult.Text = lines;
                    returnList.Add(labelGroupResult);
                }
                
                break;
        }

        return returnList;
    }
    
    public static List<DocumentLine> RestrictToPossibilities(
        IReadOnlyList<TextToMatch>? possibilities,
        IReadOnlyList<DocumentLine> lines)
    {
        if (possibilities?.Any() != true)
        {
            return lines.ToList();
        }
        
        return lines
            .Where(line => possibilities
                .Any(possibility => possibility.LineMustStartWith
                    ? line.Text.StartsWith(possibility.Text)
                    : line.Text.Contains(possibility.Text)))
            .Select(line =>
            {
                var possibility = possibilities
                    .First(possibility => possibility.LineMustStartWith
                        ? line.Text.StartsWith(possibility.Text)
                        : line.Text.Contains(possibility.Text));

                var possibilityWords = line.Columns
                    .SelectMany(c => c.Words)
                    .ToList();
                
                possibilityWords = DocumentLineColumn.FilterWordsFromText(possibilityWords, possibility.Text);
                
                var clonedLine = line.Clone();
                clonedLine.Columns.Clear();
                clonedLine.Columns.Add(new DocumentLineColumn(possibilityWords));

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
                request.licenceNumberMapping!,
                request.previouslyParsedPaths!,
                request.regionCode,
                request.processRunId,
                request.lookupConfiguration!,
                request.additionalInformationStore);
            
            if (request.label!.MinimumSubMatches.HasValue
                && request.label.MinimumSubMatches.Value > subResults.Count)
            {
                return [];
            }

            result.SubResults = subResults;
        }
        
        return results;
    }
    
    private static (bool HasPossiblites, LabelGroupResult? LabelGroupResult) RestrictToPossibility(
        FunctionInputModel request,
        LabelGroupResult result)
    {
        if (request.label!.Possibilities?.Any() != true)
        {
            return (false, result);
        }

        var possiblityFound = request.label.Possibilities.Any(possibility =>
            result.Text?.FirstOrDefault()?.Text.Contains(possibility.Text) == true);

        if (possiblityFound)
        {
            var possibility = request.label.Possibilities
                .First(possibility => result.Text!.First().Text.Contains(possibility.Text));
            
            var possibilityWords = result.Text!.First().Columns
                .SelectMany(c => c.Words)
                .ToList();
            
            possibilityWords = DocumentLineColumn.FilterWordsFromText(possibilityWords, possibility.Text);
            
            var clonedLine = result.Text!.First().Clone();
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(possibilityWords));

            var clonedResult = result.Clone();
            clonedResult.Text = [clonedLine];
            
            return (true, clonedResult);
        }

        return (true, null);
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
                    result.Text?.FirstOrDefault()?.Text.Contains(possibility.Text) == true))
            .Select(result =>
            {
                var lineText = result.Text!.First().Text;
                
                var possibility = request.label.Possibilities
                    .First(possibility => lineText.Contains(possibility.Text));

                var possibilityWords = result.Text!.First().Columns
                    .SelectMany(c => c.Words)
                    .ToList();
                
                possibilityWords = DocumentLineColumn.FilterWordsFromText(possibilityWords, possibility.Text);
                
                var clonedLine = result.Text!.First().Clone();
                clonedLine.Columns.Clear();
                clonedLine.Columns.Add(new DocumentLineColumn(possibilityWords));
                
                var clonedResult = result.Clone();
                clonedResult.Text = [clonedLine];

                return clonedResult;
            })
            .ToList();
    }
}