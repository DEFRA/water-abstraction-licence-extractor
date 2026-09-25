using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Methods;

public static class BaseMethod
{
    public static async Task<List<LabelGroupResult>> FilterIntoFormatAsync(
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
                    var (success, licenceNumberLines) = request.licenceNumberService!.AnyIsLicenceNumber(
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
                    var (success, licenceNumberLinesF) = request.licenceNumberService!.AnyIsLicenceNumber(
                        lines,
                        request.label,
                        request.isOcr,
                        request.additionalInformationStore);
                    
                    if (success)
                    {
                        var licenceNumberLines = RestrictToPossibilities(request.label?.Possibilities, licenceNumberLinesF);

                        foreach (var licenceNumberLine in licenceNumberLines)
                        {
                            var dmsFileData = await request.dmsLookupService!.GetDmsFileDataAsync(
                                licenceNumberLine.Text,
                                request.cacheService!);
                    
                            if (dmsFileData == null)
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
                var result = RestrictToPossibility(request, lines);

                if (result.HasPossiblites)
                {
                    if (result.LabelGroupResult?.Text != null)
                    {
                        labelGroupResult.Text = [result.LabelGroupResult];
                        returnList.Add(labelGroupResult);
                    }
                }
                else if (lines.Count > 0)
                {
                    // Only add a result when this candidate actually found something. An
                    // empty-but-present result here previously made downstream single-value
                    // selection stop trying the field's other alternates - e.g. Units's
                    // same-line .After("Units") alternate finding nothing blocked its own
                    // wrap-aware .Between("Units","Flow Rate") alternate from ever running.
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
                .Any(possibility =>
                {
                    var possibilityText = possibility.Text;
                    var possibilityTextWithSpaceInfront = $" {possibility.Text}";

                    var startWith = line.Text.StartsWith(possibilityText, StringComparison.OrdinalIgnoreCase);
                    
                    return possibility.LineMustStartWith
                        ? line.Text.StartsWith(possibilityText, StringComparison.OrdinalIgnoreCase)
                        : startWith ||
                            line.Text.Contains(possibilityTextWithSpaceInfront, StringComparison.OrdinalIgnoreCase);
                }))
            .Select(line =>
            {
                var possibility = possibilities
                    .First(possibility => possibility.LineMustStartWith
                        ? line.Text.StartsWith(possibility.Text, StringComparison.OrdinalIgnoreCase)
                        : line.Text.Contains(possibility.Text, StringComparison.OrdinalIgnoreCase));

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
                request.previouslyParsedPaths!,
                request.regionCode,
                request.processRunId,
                request.lookupConfiguration!,
                request.documentLineService!,
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
    
    // Contains check, but ExceptWhenInsideWord rejects a match embedded in a longer word (e.g.
    // "in" inside "Point") - otherwise a short possibility like "In" or "N" matches as a
    // coincidental substring. Boundary is letter/digit adjacency, not whitespace: some real WR51
    // fixtures glue a label to its value with no space ("supply:In Order" is one word,
    // "supply:In"), so a colon must count as a valid boundary too. Checks every occurrence, not
    // just the first, since a possibility can appear both embedded and standalone in the same
    // text. Public: also used by WrInspectionReportTableMatcher.
    public static bool MatchesPossibility(string? text, TextToMatch possibility)
    {
        if (text == null)
        {
            return false;
        }

        if (!possibility.ExceptWhenInsideWord || possibility.Text.Length == 0)
        {
            return text.Contains(possibility.Text, StringComparison.OrdinalIgnoreCase);
        }

        var searchStart = 0;

        while (searchStart <= text.Length)
        {
            var indexOf = text.IndexOf(possibility.Text, searchStart, StringComparison.OrdinalIgnoreCase);

            if (indexOf == -1)
            {
                return false;
            }

            var charBeforeIsLetterOrDigit = indexOf >= 1 && char.IsLetterOrDigit(text[indexOf - 1]);
            var charAfterIsLetterOrDigit = text.Length > indexOf + possibility.Text.Length
                && char.IsLetterOrDigit(text[indexOf + possibility.Text.Length]);

            if (!charBeforeIsLetterOrDigit && !charAfterIsLetterOrDigit)
            {
                return true;
            }

            searchStart = indexOf + 1;
        }

        return false;
    }
    
    internal static (bool HasPossiblites, DocumentLine? LabelGroupResult) RestrictToPossibility(
        FunctionInputModel request,
        IReadOnlyList<DocumentLine> lines)
    {
        if (request.label!.Possibilities?.Any() != true)
        {
            return (false, null);
        }

        foreach (var line in lines)
        {
            var possiblityFound = request.label.Possibilities.Any(possibility =>
                MatchesPossibility(line.Text, possibility));

            if (!possiblityFound)
            {
                continue;
            }

            var possibility = request.label.Possibilities
                .First(possibility => MatchesPossibility(line.Text, possibility));

            var possibilityWords = line.Columns
                .SelectMany(c => c.Words)
                .ToList();

            possibilityWords = DocumentLineColumn.FilterWordsFromText(possibilityWords, possibility.Text);

            var clonedLine = line.Clone();
            clonedLine.Columns.Clear();
            clonedLine.Columns.Add(new DocumentLineColumn(possibilityWords));

            return (true, clonedLine);
        }
        
        var firstLineText = lines.FirstOrDefault()?.Text;
        
        // A field with no answer produces zero captured lines, not one empty-text line, so the
        // "" catch-all possibility (a genuinely blank tick field) never reaches the Contains
        // check above. Without this, that case looks identical to "label not found" downstream.
        if (string.IsNullOrEmpty(firstLineText)
            && request.label.Possibilities.Any(possibility => possibility.Text.Length == 0))
        {
            return (true, new DocumentLine());
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
                    result.Text?.FirstOrDefault()?.Text.Contains(possibility.Text, StringComparison.OrdinalIgnoreCase) == true))
            .Select(result =>
            {
                var lineText = result.Text!.First().Text;
                
                var possibility = request.label.Possibilities
                    .First(possibility => lineText.Contains(possibility.Text, StringComparison.OrdinalIgnoreCase));

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