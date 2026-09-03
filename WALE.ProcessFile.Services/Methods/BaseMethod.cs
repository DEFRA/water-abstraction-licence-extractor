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
                var result = RestrictToPossibility(request, labelGroupResult);
                if (result?.Text != null) returnList.Add(result);

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
    
    // Whether `possibility.Text` genuinely appears in `text` - a plain Contains check, unless
    // the possibility opted into ExceptWhenInsideWord (an existing flag - DataHelper.
    // RemoveExcludes already uses it for a related idea, but with whitespace-adjacency rather
    // than the letter/digit-adjacency here; the two turned out not to be interchangeable, see
    // below), in which case a match embedded inside a longer word doesn't count. Without this,
    // a short possibility like "In" or "N" matches as a coincidental substring of unrelated
    // text - e.g. "Point of abstraction:" (wrongly captured due to a separate next-line
    // column-matching bug) contains "in" inside "Point", which was silently accepted as a
    // genuine "In Order" answer.
    //
    // Boundary is letter/digit adjacency, not whitespace adjacency: several real WR51 fixtures
    // render a field's own label and value glued with no space at all - e.g. "Source of
    // supply:In Order" is one PDF word token "supply:In" - so treating any non-whitespace
    // character (a label's own trailing colon included) as "inside a word" rejected genuine
    // answers. Punctuation like ":" counts as a valid boundary; only being glued to another
    // letter or digit (e.g. "in" inside "Point") counts as embedded.
    //
    // Checks every occurrence, not just the first, since a possibility can appear both
    // embedded (invalid) and standalone (valid) in the same text.
    private static bool MatchesPossibility(string? text, TextToMatch possibility)
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

    internal static LabelGroupResult? RestrictToPossibility(
        FunctionInputModel request,
        LabelGroupResult result)
    {
        if (request.label!.Possibilities?.Any() != true)
        {
            return result;
        }

        var firstLineText = result.Text?.FirstOrDefault()?.Text;

        var possiblityFound = request.label.Possibilities.Any(possibility =>
            MatchesPossibility(firstLineText, possibility));

        if (possiblityFound)
        {
            var possibility = request.label.Possibilities
                .First(possibility => MatchesPossibility(result.Text!.First().Text, possibility));

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
        }

        // A field with no answer on the page produces zero captured lines, not one line with
        // empty text - so the "" catch-all possibility (added so a genuinely blank tick field
        // still survives as a match, e.g. WrInspectionReportLabelConfiguration.GetInOrderField)
        // never gets a chance to match via the Contains check above, since FirstOrDefault() on
        // an empty list is null. Without this, the whole match silently vanishes and looks
        // identical to "the label was never found at all" downstream, rather than "found, but
        // genuinely blank".
        if (string.IsNullOrEmpty(firstLineText)
            && request.label.Possibilities.Any(possibility => possibility.Text.Length == 0))
        {
            var clonedResult = result.Clone();
            clonedResult.Text = [];

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