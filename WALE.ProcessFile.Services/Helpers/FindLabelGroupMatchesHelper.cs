using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Methods;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services;

namespace WALE.ProcessFile.Services.Helpers;

public static class FindLabelGroupMatchesHelper
{
    public static async Task<IReadOnlyList<LabelGroupResult>> FindLabelGroupMatchesInLinesAsync(
        IReadOnlyList<DocumentLineWrapped> lines,
        IReadOnlyList<LabelToMatch> labels,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        IReadOnlyList<LabelGroupResult> siblingMatches,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        PdfDataExtractorService pdfDataExtractorService,
        IDocumentLineService documentLineService,
        Dictionary<string, object?> additionalInformationStore)
    {
        var returnList = new List<LabelGroupResult>();

        var lineCount = -1;
        var totalLineCount = lines.Count;
        
        foreach (var line in lines)
        {
            var fullLine = line.Line;
            var breakLineLoop = false;
            
            foreach (var label in labels.Where(whereLabel => !whereLabel.Completed)) // TODO we should change this to just accept one label
            {
                var partialLine = fullLine;
                DocumentLine? previousPartialLine = null;

                IReadOnlyList<DocumentLine>? previousLines = null;
                IReadOnlyList<DocumentLine>? nextLines = null;
                
                lineCount += 1;

                while (partialLine?.Columns.Any(c => c.Text.Length > 0) == true)
                {
                    if (previousPartialLine?.Text == partialLine.Text)
                    {
                        throw new Exception("Infinite loop detected - coding error");
                    }
                    
                    previousPartialLine = partialLine;
                    
                    var textBeforeAtAndAfterLabel = new List<TextAndLabelAndPosition>();
                    var continuePartialLoop = false;
                    var matchedLabel = label;

                    switch (label.Format)
                    {
                        case LinkedLicenceDontInline.Constant:
                            partialLine = null;
                            continue;
                        case LinkedLicence.Constant:
                        {
                            var linkedLicences = await ProcessLinkedLicenceAsync(
                                partialLine,
                                siblingMatches,
                                label,
                                previouslyParsedPaths,
                                regionCode,
                                processRunId,
                                lookupConfiguration,
                                pdfDataExtractorService);

                            returnList.AddRange(linkedLicences);

                            partialLine = null;
                            continue;
                        }
                    }
                    
                    if (FormattingHelper.IsLineEmpty(partialLine)
                        && label.TextToMatch?.Any(text =>
                            text.Text.Equals("[START_OF_BLOCK]", StringComparison.OrdinalIgnoreCase)) != true
                        && !(label.Position == LabelPosition.SplitAtLabel && lineCount == totalLineCount - 1))
                    {
                        partialLine = null;
                        continue;
                    }
                    
                    TextToMatch? matchedStartText = null;

                    var labelStartPageNumber = partialLine.PageNumber;
                    var labelStartLineNumber = partialLine.LineNumber;
                    var labelStartCharIndex = 0;
                    var labelEndPageNumber = partialLine.PageNumber;
                    var labelEndLineNumber = partialLine.LineNumber;
                    var labelEndCharIndex = 0;

                    var labelTextLookingForSingleLine = label.Text?
                        .Where(t => t.SingleLinePerItem)
                        .ToList();
                    
                    var lookingForSingleLine = labelTextLookingForSingleLine?.Count >= 1;
                    var rulePassed = false;
                    
                    if (lookingForSingleLine)
                    {
                        nextLines ??= line.NextLines(lines, label);
                        var nextLine = nextLines.FirstOrDefault();
                        
                        var thisLineStartsWithCapital = char.IsUpper(partialLine.Text[0]);
                        var thisIsLastLine = nextLine == null;
                        var nextLineStartsWithCapital = !thisIsLastLine
                            && nextLines.Count >= 1
                            && !string.IsNullOrEmpty(nextLine?.Text)
                            && char.IsUpper(nextLine.Text[0]);

                        const int maxNoneWrappedLineLength = 60;
                        var lineIsNotWrapping = partialLine.Text.Length <= maxNoneWrappedLineLength;
                        
                        var matchesRule = thisLineStartsWithCapital
                            && lineIsNotWrapping
                            && (nextLineStartsWithCapital || thisIsLastLine);

                        if (matchesRule)
                        {
                            rulePassed = true;
                            label.Text = [label.Text!.First(lt => lt.SingleLinePerItem)];
                            matchedStartText = label.Text.Single();
                            
                            // Clear out the next lines, as we are doing it in isolation
                            nextLines = [];
                        }
                        else
                        {
                            var anyNotLookingForSingleLine = label.TextToMatch?.Count >= 1;

                            if (!anyNotLookingForSingleLine)
                            {
                                partialLine = null;
                                continue;
                            }
                        }
                    }
                    
                    if (rulePassed)
                    {
                        // Skip through to the next step
                    }
                    else if (label.TextToMatch?.Any() == true)
                    {
                        nextLines ??= line.NextLines(lines, label);
                        var nextLine = nextLines.FirstOrDefault();
                        
                        if (!LabelMatchingHelper.LineContainsLabel(
                            partialLine,
                            nextLine,
                            fullLine!,
                            label.TextToMatch,
                            label.Position,
                            lineCount,
                            totalLineCount,
                            out matchedStartText,
                            out labelStartPageNumber,
                            out labelStartLineNumber,
                            out labelStartCharIndex,
                            out labelEndPageNumber,
                            out labelEndLineNumber,
                            out labelEndCharIndex))
                        {
                            partialLine = null;
                            continue;
                        }
                    }
                    else if (label.Possibilities?.Any() == true && label.Format == "Text")
                    {
                        var matchedPossibilities =
                            BaseMethod.RestrictToPossibilities(label.Possibilities, [partialLine]); 
                        
                        if (matchedPossibilities.Count == 0)
                        {
                            partialLine = null;
                            continue;
                        }

                        matchedStartText = new TextToMatch(matchedPossibilities[0].Text);
                    }
                    
                    if (LabelMatchingHelper.ShouldSkipLineAsForbidden(partialLine.Text, label))
                    {
                        partialLine = null;
                        continue;
                    }

                    if (label.MatchAllText)
                    {
                        previousLines ??= line.PreviousLines(lines, label);
                        nextLines ??= line.NextLines(lines, label);

                        if (NotMatchedAll(partialLine, fullLine!, label, lineCount, previousLines, nextLines))
                        {
                            partialLine = null;
                            continue;
                        }
                    }
                    else
                    {
                        matchedLabel = label.Clone();

                        if (matchedStartText != null)
                        {
                            matchedLabel.Text = [matchedStartText];
                        }
                    }
                    
                    var over2Lines = labelEndPageNumber > labelStartPageNumber
                        || (labelEndPageNumber == labelStartPageNumber && labelEndLineNumber > labelStartLineNumber);
                    
                    DocumentLine? nextLine2 = null;
                    
                    if (over2Lines)
                    {
                        nextLines ??= line.NextLines(lines, label);
                        nextLine2 = nextLines.FirstOrDefault();
                    }
                    
                    textBeforeAtAndAfterLabel.AddRange(
                        GetLineBeforeAtAndAfterText(
                            partialLine,
                            nextLine2,
                            matchedLabel));
                    
                    var lookupExpressions = GetRelevantLookupExpressions(matchedLabel)
                        .ToList();
                    
                    var labelGroupResult = new LabelGroupResult
                    {
                        IsOcr = isOcr,
                        LabelStartPageNumber = labelStartPageNumber,
                        LabelStartLineNumber = labelStartLineNumber,
                        LabelStartCharPosition = labelStartCharIndex,
                        LabelEndPageNumber = labelEndPageNumber,
                        LabelEndLineNumber = labelEndLineNumber,
                        LabelEndCharPosition = labelEndCharIndex,                        
                        ServiceName = serviceName
                    };
                    
                    previousLines ??= line.PreviousLines(lines, label);
                    nextLines ??= line.NextLines(lines, label);
                    
                    var request = new FunctionInputModel
                    {
                        actsLikeSingleWord = matchedLabel.Format == ActsLikeSingleWord.Constant,
                        textBeforeAtAndAfterLabel = textBeforeAtAndAfterLabel,
                        isCompanyType = matchedLabel.Format == CompanyName.Constant,
                        isDateLookup = matchedLabel.Format == Date.Constant,
                        isDateOrPurposeLookup = matchedLabel.Format == DateOrPurpose.Constant,
                        isLicenceNumberLookup = matchedLabel.Format == LicenceNumber.Constant,
                        isNumberLookup = matchedLabel.Format == Number.Constant,
                        isOcr = isOcr,
                        label = matchedLabel,
                        labelGroupName = labelGroupName,
                        labelGroupResult = labelGroupResult,
                        pdfDataExtractorService = pdfDataExtractorService,
                        previouslyParsedPaths = previouslyParsedPaths,
                        previousLines = previousLines,
                        nextLines = nextLines,
                        serviceName = serviceName,
                        siblingMatches = siblingMatches,
                        outputService = lookupConfiguration.OutputService,
                        cacheService = lookupConfiguration.CacheService,
                        licenceNumberService = lookupConfiguration.LicenceNumberService,
                        isSingleWord = matchedLabel.Format == SingleWord.Constant,
                        isUnitsLookup = matchedLabel.Format == Units.Constant,
                        line = partialLine,
                        lineForPosition = fullLine,
                        lineNumber = partialLine.LineNumber,
                        processRunId = processRunId,
                        regionCode = regionCode,
                        lookupConfiguration = lookupConfiguration,
                        documentLineService = documentLineService,
                        additionalInformationStore = additionalInformationStore
                    };
                    
                    var singleValueWanted = matchedLabel.MultipleMatchBehaviour is
                        MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValue
                        or MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel;
                    
                    foreach (var expression in lookupExpressions)
                    {
                        var dtStart = DateTime.Now;
                        
                        var result = await ProcessExpressionResultAsync(
                            expression.Value,
                            request,
                            partialLine!,
                            singleValueWanted);
                        
                        if ((DateTime.Now - dtStart).TotalMilliseconds > 100)
                        {
                            ConsoleHelper.WriteLine(
                                $"INFO - {nameof(FindLabelGroupMatchesHelper)} - ProcessExpressionResultAsync ({request.label.Name}, {expression.Key}) took {(DateTime.Now - dtStart).TotalMilliseconds}ms");
                        }
                        
                        var itsAFailedSplitAndWeHaveSucessfullySplitAlready =
                            label.Position == LabelPosition.SplitAtLabel
                            && result.Results.Count == 1
                            && returnList.Count > 1;

                        if (itsAFailedSplitAndWeHaveSucessfullySplitAlready)
                        {
                            break;
                        }
                        
                        if (request.label.FindMultipleOnSingleLine
                            && request.textBeforeAtAndAfterLabel.Count >= 1
                            && request.label.Position is not LabelPosition.SplitAtLabel
                            and not LabelPosition.TextToFindIsBetweenLabels
                            and not LabelPosition.RelatedCategoryPosition)
                        {
                            var clonedRequest = request.Clone();
                            
                            var matchBefore = clonedRequest.textBeforeAtAndAfterLabel?.FirstOrDefault(x =>
                                x.Label?.Position == LabelPosition.LabelIsAfterTextToFind);

                            if (matchBefore != null)
                            {
                                clonedRequest.textBeforeAtAndAfterLabel?.Remove(matchBefore);
                            }
                            
                            var additionalResults = await ProcessExpressionResultAsync(
                                AfterTextContainsAnotherMatch.FunctionAsync,
                                clonedRequest,
                                partialLine!,
                                singleValueWanted);
                            
                            result.Results.AddRange(additionalResults.Results);
                            result.Results = FilterDownResults(result.Results, request.label);
                        }
                        
                        if (result.Continue)
                        {
                            continue;
                        }
                        
                        if (result.Return)
                        {
                            return result.Results;
                        }
                        
                        if (result.ContinuePartialLoop)
                        {
                            if (result.NewPartialLine == null)
                            {
                                partialLine = null;
                            }
                            
                            continuePartialLoop = true;
                        }
                        
                        returnList.AddRange(result.Results);
                        
                        if (result.Break)
                        {
                            break;
                        }

                        returnList = FilterDownResults(returnList, request.label);
                        
                        if (result.NewPartialLine != null)
                        {
                            partialLine = result.NewPartialLine;
                        }
                    }

                    if (continuePartialLoop)
                    {
                        continue;
                    }

                    partialLine = null;
                }
            }
            
            if (breakLineLoop)
            {
                break;
            }
        }

        var atLeastOneResultFound = returnList.Count > 1;
        
        var allAreSingleLabelMultipleLines = returnList.All(match =>
            match.MatchedLabel?.MultipleMatchBehaviour is
                MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines);

        if (atLeastOneResultFound && allAreSingleLabelMultipleLines)
        {
            var textList = new List<DocumentLine>();

            foreach (var returnListLoop in returnList)
            {
                textList.AddRange(returnListLoop.Text!);
            }

            var returnItem = returnList.First();
            
            return
            [
                new()
                {
                    MatchedLabel = returnItem.MatchedLabel!.Clone(),
                    LabelGroupName = returnItem.LabelGroupName,
                    MatchedPosition = returnItem.MatchedPosition,
                    LabelStartPageNumber = returnItem.LabelStartPageNumber,
                    LabelStartLineNumber = returnItem.LabelStartLineNumber,
                    LabelEndPageNumber = returnItem.LabelEndPageNumber,
                    LabelEndLineNumber = returnItem.LabelEndLineNumber,                    
                    ServiceName = returnItem.ServiceName,
                    Text = textList
                }
            ];
        }
        
        return returnList;
    }
    
    
    private static async Task<IReadOnlyList<LabelGroupResult>> ProcessLinkedLicenceAsync(
        DocumentLine line,
        IReadOnlyList<LabelGroupResult> siblingMatches,
        LabelToMatch label,
        List<string> previouslyParsedFiles,
        int regionCode,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        PdfDataExtractorService pdfDataExtractorService)
    {
        var returnList = new List<LabelGroupResult>();
        
        var licenceNumbers = siblingMatches
            .Where(siblingMatch => siblingMatch.MatchedLabel?.Name == label.RelatedName)
            .Select(result => result.Text?.FirstOrDefault())
            .ToList();
        
        var pathsToFetch = new List<(string RelatedFileName, string LicenceNumber)>();
        
        foreach (var licenceNumber in licenceNumbers)
        {
            if (string.IsNullOrEmpty(licenceNumber?.Text))
            {
                continue;
            }
            
            var dmsFileData = await FormattingHelper.GetDmsFileDataAsync(licenceNumber.Text, lookupConfiguration.CacheService);
                    
            if (dmsFileData == null)
            {
                continue;
            }
            
            var destinationFilenames = dmsFileData.DestinationFileName!;
                
            if (previouslyParsedFiles.Contains(destinationFilenames))
            {
                continue;
            }

            previouslyParsedFiles.Add(destinationFilenames);
            pathsToFetch.Add((destinationFilenames, licenceNumber.Text));
        }

        foreach (var (relatedFileName, relatedLicenceNumber) in pathsToFetch)
        {
            if (!File.Exists(relatedFileName))
            {
                continue;
            }

            var clonedConfig = lookupConfiguration.Clone();
            clonedConfig.RegionId = regionCode;
            
            var linkedDmsFileData = await FormattingHelper.GetDmsFileDataAsync(
                relatedLicenceNumber,
                lookupConfiguration.CacheService);

            if (linkedDmsFileData == null)
            {
                ConsoleHelper.WriteLine(
                    $"INFO - {nameof(FindLabelGroupMatchesHelper)} - ProcessLinkedLicenceAsync - excluding file as doesn't have file id set");
                
                break;
            }

            (bool StopExecution, bool? AlreadySaved, MatchesResult? Item) relatedFileMatches;
            
            try
            {
                relatedFileMatches = await pdfDataExtractorService.GetMatchesAsync(
                    relatedFileName,
                    linkedDmsFileData,
                    clonedConfig,
                    previouslyParsedFiles,
                    processRunId);

                if (relatedFileMatches.StopExecution)
                {
                    continue;
                }
                
                ConsoleHelper.WriteLine($"INFO - {nameof(FindLabelGroupMatchesHelper)} - Finished/released lock/saving for {linkedDmsFileData.FileId}");

                if (relatedFileMatches.AlreadySaved != true && lookupConfiguration.UseLockExclusivity)
                {
                    await pdfDataExtractorService.SaveMatchResultAsync(
                        relatedFileMatches.Item!,
                        linkedDmsFileData.FileId,
                        processRunId);
                }
            }
            catch (Exception ex)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(FindLabelGroupMatchesHelper)} - {linkedDmsFileData.FileId} had error, releasing lock. {ex}");
                
                await lookupConfiguration.OutputService.SaveErrorMatchesResultAsync(
                    relatedFileName,
                    linkedDmsFileData.FileId,
                    processRunId,
                    ex.ToString());
                
                throw;
            }
            
            if (relatedFileMatches.StopExecution)
            {
                continue;
            }

            var labelResult = new LabelGroupResult
            {
                MatchedLabel = label,
                SubResults = relatedFileMatches.Item!.Matches!,
                LabelStartPageNumber = line.PageNumber
            };

            FormattingHelper.RemoveRemoves(labelResult, []); // TODO do this properly at some point
            returnList.Add(labelResult);
        }

        if (pathsToFetch.Count > 0)
        {
            label.Completed = true;
        }

        return returnList;
    }
    
    private static bool NotMatchedAll(
        DocumentLine line,
        DocumentLine lineForPosition,
        LabelToMatch label,
        int lineCount,
        IReadOnlyList<DocumentLine> previousLines,
        IReadOnlyList<DocumentLine> nextLines)
    {
        var matchedAll = true;
                    
        foreach (var labelText in label.TextToMatch!)
        {
            var nextLineTemp = nextLines.FirstOrDefault();
            
            if (LabelMatchingHelper.LineContainsLabel(
                line,
                nextLineTemp,
                lineForPosition,
                [labelText],
                label.Position,
                lineCount,
                PositionConstants.UnknownLinesTotal,
                out _,
                out _,
                out _,
                out _,          
                out _,
                out _,
                out _))
            {
                continue;
            }

            var continueOuterLoop = false;
            var count = 0;
            
            foreach (var previousLine in previousLines)
            {
                var previousPreviousLine = previousLines.Count > count + 1 ?
                    previousLines[count + 1]
                    : null;

                count += 1;
                
                if (LabelMatchingHelper.LineContainsLabel(
                    previousLine,
                    previousPreviousLine,
                    previousLine,
                    [labelText],
                    label.Position,
                    lineCount,
                    PositionConstants.UnknownLinesTotal,
                    out _,
                    out _,
                    out _,
                    out _,          
                    out _,
                    out _,
                    out _))
                {
                    continueOuterLoop = true;
                    break;
                }
            }

            count = 0;
                        
            foreach (var nextLine in nextLines)
            {
                var nextNextLine = nextLines.Count > count + 1 ?
                    nextLines[count + 1]
                    : null;

                count += 1;
                
                if (LabelMatchingHelper.LineContainsLabel(
                    nextLine,
                    nextNextLine,
                    nextLine,
                    [labelText],
                    label.Position,
                    lineCount,
                    PositionConstants.UnknownLinesTotal,
                    out _,
                    out _,
                    out _,
                    out _,          
                    out _,
                    out _,
                    out _))
                {
                    continueOuterLoop = true;
                    break;
                }
            }

            if (continueOuterLoop)
            {
                continue;
            }
                        
            matchedAll = false;
            break;
        }

        if (!matchedAll)
        {
            return true;
        }
        
        return false;
    }
    
    private static bool IsStartOfBlock(LabelToMatch label)
    {
        return label.TextToMatch?
            .Select(t => t.Text)
            .Contains("[START_OF_BLOCK]") == true;
    }
    
    private static List<TextAndLabelAndPosition> GetLineBeforeAtAndAfterText(
        DocumentLine line,
        DocumentLine? nextLineToContinueOnto,
        LabelToMatch label)
    {
        var returnItems = new List<TextAndLabelAndPosition>();
        
        var lineColumns = line.Columns
            .Select(c => c.Text)
            .ToList();

        if (label.TextToMatch == null || IsStartOfBlock(label))
        {
            returnItems.Add(new TextAndLabelAndPosition
            {
                ColumnsText = lineColumns,
                Label = label,
                Position = "StartOfBlock"
            });

            return returnItems;
        }

        var combinedText = nextLineToContinueOnto != null ?
            $"{line.Text} {nextLineToContinueOnto.Text}"
            : line.Text;
        
        if (label.TextToMatch?.FirstOrDefault()?.Regex != null &&
            label.Position == LabelPosition.LabelIsActuallyResult)
        {
            var regex = label.TextToMatch.FirstOrDefault()!.Regex;
            var matches = regex!.Matches(combinedText);

            foreach (var match in matches.AsQueryable())
            {
                var regexValue = match.Value;
                var positionIndexOnLine = combinedText.IndexOf(regexValue, StringComparison.Ordinal);

                if (positionIndexOnLine > 0)
                {
                    var previousChar = combinedText[positionIndexOnLine - 1];
                    var firstChar = match.Value[0];
                    
                    if (previousChar != ' ' && previousChar != ',' && previousChar != '.'
                        && firstChar != ' ' && firstChar != ',' && firstChar != '.')
                    {
                        continue;
                    }
                }

                var valueStartPositionOnLine = combinedText.IndexOf(
                    regexValue,
                    StringComparison.OrdinalIgnoreCase);
                
                var valueEndPositionOnLine = valueStartPositionOnLine + regexValue.Length;

                var beforeColumns = new List<string>();
                var valueColumns = new List<string>();
                var afterColumns = new List<string>();
                
                var totalLengthBeforeThisColumn = 0;
                
                foreach (var lineColumn in lineColumns)
                {
                    var totalLengthSoFarExcludingThisColumn =
                        beforeColumns.Sum(bc => 1 + bc.Length)
                        + valueColumns.Sum(vc => 1 + vc.Length)
                        + afterColumns.Sum(ac => 1 + ac.Length);
                    
                    var totalLengthSoFarIncludingThisColumn = lineColumn.Length
                        + totalLengthSoFarExcludingThisColumn;
                    
                    // Our value starts at or past the end of this column
                    if (valueStartPositionOnLine >= totalLengthSoFarIncludingThisColumn)
                    {
                        if (!string.IsNullOrWhiteSpace(lineColumn))
                        {
                            beforeColumns.Add(lineColumn);
                        }
                    }
                    // We've seen past the point of the value now
                    else if (totalLengthSoFarExcludingThisColumn > valueEndPositionOnLine)
                    {
                        if (!string.IsNullOrWhiteSpace(lineColumn))
                        {
                            afterColumns.Add(lineColumn);
                        }
                    }
                    // Our value starts before the end of this column (partial)
                    else if (valueStartPositionOnLine < totalLengthSoFarIncludingThisColumn)
                    {
                        var newPos = valueStartPositionOnLine - totalLengthBeforeThisColumn;
                        var cutoffLength = regexValue.Length;
                        
                        if (newPos < 0)
                        {
                            cutoffLength += newPos;
                            newPos = 0;
                        }

                        var beforeText = lineColumn[..newPos];
                        if (!string.IsNullOrWhiteSpace(beforeText))
                        {
                            beforeColumns.Add(beforeText);
                        }

                        var val = lineColumn[newPos..];
                        
                        if (val.Length > cutoffLength)
                        {
                            var afterText = val[cutoffLength..];

                            if (!string.IsNullOrWhiteSpace(afterText))
                            {
                                afterColumns.Add(afterText);
                            }

                            val = val[..cutoffLength];
                        }

                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            valueColumns.Add(val);
                        }
                    }

                    totalLengthBeforeThisColumn = totalLengthSoFarIncludingThisColumn + 1;
                }

                if (beforeColumns.Count > 1 || !string.IsNullOrWhiteSpace(beforeColumns.FirstOrDefault()))
                {
                    var beforeLabel = label.Clone();
                    beforeLabel.Position = LabelPosition.LabelIsAfterTextToFind;
                    
                    returnItems.Add(new TextAndLabelAndPosition
                    {
                        ColumnsText = beforeColumns,
                        Label = beforeLabel,
                        Position = "BeforeLabel"
                    });
                }
                
                returnItems.Add(new TextAndLabelAndPosition
                {
                    ColumnsText = valueColumns,
                    Label = label,
                    Position = "AtLabel"
                });

                if (afterColumns.Count > 1 || !string.IsNullOrWhiteSpace(afterColumns.FirstOrDefault()))
                {
                    var afterLabel = label.Clone();
                    afterLabel.Position = LabelPosition.LabelIsBeforeTextToFind;

                    returnItems.Add(new TextAndLabelAndPosition
                    {
                        ColumnsText = afterColumns,
                        Label = afterLabel,
                        Position = "AfterLabel"
                    });
                }
            }

            return returnItems;
        }
        
        var labelTextPositionIndex = PositionConstants.PositionNotFound;
        string? matchedLabelText = null;

        foreach (var labelText in label.TextToMatch!)
        {
            var index = combinedText.IndexOf(
                labelText.Text,
                StringComparison.OrdinalIgnoreCase);

            if (index > PositionConstants.PositionNotFound)
            {
                labelTextPositionIndex = index;
                matchedLabelText = labelText.Text;

                break;
            }
        }

        if (labelTextPositionIndex == PositionConstants.PositionNotFound)
        {
            return [];
        }

        var textBeforeLabel = combinedText[..labelTextPositionIndex];
        var textAtLabel = matchedLabelText;
        var textAfterLabel = combinedText[(labelTextPositionIndex + matchedLabelText!.Length)..];

        if (!string.IsNullOrEmpty(textBeforeLabel)
            && label.Position is LabelPosition.LabelIsAfterTextToFind
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.LabelIsInMiddleOfTextToFind
                or LabelPosition.TextToFindIsBetweenLabels
                or LabelPosition.ContractIsSuccession
                or LabelPosition.RelatedCategoryPosition
                or LabelPosition.ApplicableToMost
                or LabelPosition.SplitAtLabel)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = label.Position is
                LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels
                ? LabelPosition.LabelIsAfterTextToFind
                : label.Position;

            returnItems.Add(new TextAndLabelAndPosition
            {
                ColumnsText = [textBeforeLabel],
                Label = returnLabel,
                Position = "BeforeLabel"
            });
        }

        if (!string.IsNullOrEmpty(textAtLabel) &&
            (label.IncludeStartLabelText || label.Possibilities?.Any() == true))
        {
            var returnLabel = label.Clone();
            returnLabel.Position = LabelPosition.LabelIsActuallyResult;

            returnItems.Add(new TextAndLabelAndPosition
            {
                ColumnsText = [textAtLabel],
                Label = returnLabel,
                Position = "AtLabel"
            });
        }

        if (!string.IsNullOrEmpty(textAfterLabel)
            && label.Position is LabelPosition.LabelIsBeforeTextToFind
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.LabelIsInMiddleOfTextToFind
                or LabelPosition.TextToFindIsBetweenLabels
                or LabelPosition.ContractIsSuccession
                or LabelPosition.RelatedCategoryPosition
                or LabelPosition.ApplicableToMost
                or LabelPosition.SplitAtLabel)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = label.Position is
                LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels
                ? LabelPosition.LabelIsBeforeTextToFind
                : label.Position;

            returnItems.Add(new TextAndLabelAndPosition
            {
                ColumnsText = [textAfterLabel],
                Label = returnLabel,
                Position = "AfterLabel"
            });
        }

        return returnItems;
    }
    
    
    private static Dictionary<LabelPosition, Func<FunctionInputModel, Task<List<LabelGroupResult>>>>
        GetRelevantLookupExpressions(LabelToMatch label)
    {
        var expressions = new List<(
            LabelPosition Position,
            Func<
                FunctionInputModel,
                Task<List<LabelGroupResult>>> ResultIfMatched,
            int Order)>
        {
            (LabelPosition.ApplicableToMost, ApplicableToMost.FunctionAsync, 0),
            (LabelPosition.SplitAtLabel, SplitAtLabel.FunctionAsync, 0),
            (LabelPosition.RelatedCategoryPosition, RelatedCategoryPosition.FunctionAsync, 0),
            (LabelPosition.TextToFindIsBetweenLabels, TextToFindIsBetweenLabels.FunctionAsync, 0),
            (LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore, LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore.FunctionAsync, -1),
            (LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter, LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter.FunctionAsync, -1),
            (LabelPosition.LabelIsInMiddleOfTextToFind, LabelIsInMiddleOfTextToFind.FunctionAsync, -1),
            (LabelPosition.LabelIsBeforeTextToFind, LabelIsBeforeTextToFind.FunctionAsync, 0),
            (LabelPosition.LabelIsAfterTextToFind, LabelIsAfterTextToFind.FunctionAsync, 1)
        };

        return expressions
            .Where(expression =>
            {
                switch (label.Position)
                {
                    case LabelPosition.ContractIsSuccession
                        when expression.Position is LabelPosition.ContractIsSuccession
                            or LabelPosition.LabelIsBeforeTextToFind
                            or LabelPosition.LabelIsAfterTextToFind:
                    case LabelPosition.TextToFindIsBetweenLabels
                        when expression.Position == LabelPosition.TextToFindIsBetweenLabels:
                        return true;
                    case LabelPosition.RelatedCategoryPosition
                        when expression.Position is LabelPosition.RelatedCategoryPosition:
                        return true;
                    case LabelPosition.SplitAtLabel
                        when expression.Position is LabelPosition.SplitAtLabel:
                        return true;
                    case LabelPosition.LabelIsBeforeTextToFind
                        when expression.Position is LabelPosition.LabelIsBeforeTextToFind:
                    case LabelPosition.LabelIsAfterTextToFind
                        when expression.Position is LabelPosition.LabelIsAfterTextToFind:
                    case LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                        when expression.Position is LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                            or LabelPosition.LabelIsBeforeTextToFind
                            or LabelPosition.LabelIsAfterTextToFind:
                    case LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                        when expression.Position is LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                            or LabelPosition.LabelIsBeforeTextToFind
                            or LabelPosition.LabelIsAfterTextToFind:
                    case LabelPosition.LabelIsInMiddleOfTextToFind
                        when expression.Position is LabelPosition.LabelIsInMiddleOfTextToFind:
                        return true;
                    default:
                        return expression.Position is LabelPosition.ApplicableToMost
                           && label.Position != LabelPosition.SplitAtLabel
                           && label.Position != LabelPosition.RelatedCategoryPosition
                           && label.Position != LabelPosition.TextToFindIsBetweenLabels;
                }
            })
            .OrderBy(expression =>
            {
                if (expression.Position == LabelPosition.ApplicableToMost)
                {
                    const int minimumPositionForOrderingAscending = -1;
                    return minimumPositionForOrderingAscending;
                }

                return label.Position switch
                {
                    LabelPosition.TextToFindIsBetweenLabels => -2,
                    LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore =>
                        expression.Position is LabelPosition.LabelIsBeforeTextToFind
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                            ? -0.25
                            : 1,
                    LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter =>
                        expression.Position is LabelPosition.LabelIsAfterTextToFind
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                            ? -0.25
                            : 1,
                    LabelPosition.LabelIsInMiddleOfTextToFind =>
                        expression.Position is LabelPosition.LabelIsInMiddleOfTextToFind ? -0.25 : 1,
                    LabelPosition.LabelIsBeforeTextToFind or LabelPosition.ContractIsSuccession
                        => expression.Position is LabelPosition.LabelIsBeforeTextToFind ? 0 : 1,
                    _ => expression.Position == LabelPosition.LabelIsAfterTextToFind ? 0 : 1
                };
            })
            .ThenBy(expression => expression.Order)
            .Select(expression => (expression.Position, expression.ResultIfMatched))
            .ToDictionary(x => x.Position, x => x.ResultIfMatched);
    }
    
    private static List<LabelGroupResult> FilterDownResults(List<LabelGroupResult> returnList, LabelToMatch? label)
    {
        // De-dupe exact matches
        returnList = returnList
            .GroupBy(x => x.MatchedLabel!.FindMultipleOnSingleLine ?
                $"{x.LabelStartPageNumber}_{x.LabelStartLineNumber}_{x.LabelStartCharPosition}_{x.MatchedLabel?.Name}_{x.Text?.FirstOrDefault()?.Text}"
                : $"{x.LabelStartPageNumber}_{x.LabelStartLineNumber}_{x.MatchedLabel?.Name}_{x.Text?.FirstOrDefault()?.Text}")
            .Select(x => x.OrderByDescending(y => y.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text == "[START_OF_BLOCK]" ? 0 : 1).First())
            .ToList();
        
        var ifMultiplePreferLast = label!.TextToMatch?.FirstOrDefault()?.IfMultiplePreferLast ?? false;
        var ifMultiplePreferLongest =
            label.TextToMatch?.FirstOrDefault()?.IfMultiplePreferLongest ?? false;

        // TOOD there should only be one below - not 2 or more
        if (!ifMultiplePreferLast && !ifMultiplePreferLongest) return returnList;
        
        var alreadyOutput = returnList
            .Where(r => r.MatchedLabel?.Name == label.Name)
            .ToList();

        if (alreadyOutput.Count >= 2)
        {
            var i = alreadyOutput
                .OrderBy(x =>
                    ifMultiplePreferLast ? ((x.LabelStartPageNumber * 100) + x.LabelStartLineNumber) : x.Text?.Count)
                .First();

            returnList.Remove(i);
        }

        return returnList;
    }
    
    private static async Task<ExpressionResult> ProcessExpressionResultAsync(
        Func<FunctionInputModel, Task<List<LabelGroupResult>>> expression,
        FunctionInputModel request,
        DocumentLine partialLine,
        bool singleValueWanted)
    {
        var returnList = new List<LabelGroupResult>();
        var results = await expression(request);
        
        var continuePartialLoop = false;
        DocumentLine? newPartialLine = null;
                        
        if (results.Count == 0)
        {
            return new ExpressionResult
            {
                Continue = true
            };
        }

        // TODO the below has some weird behaviour with line numbers 33 vs 32, but is kept for now
        // as some tests depend upon it
        foreach (var result in results)
        {
            var newLineNumber = result.Text?.FirstOrDefault()?.LineNumber;

            if (newLineNumber.HasValue && newLineNumber != result.LabelStartLineNumber)
            {
                result.LabelStartLineNumber = newLineNumber.Value;
            }
        }

        if (singleValueWanted && results.Count >= 1)
        {
            if (request.label!.MultipleMatchBehaviour is
                MultipleMatchBehaviour.FindSingleInstanceOfLabelWithASingleValue)
            {
                // Prefer ones that have some text (important in split logic)
                var singleValueResult = new List<LabelGroupResult>
                {
                    results.OrderByDescending(x => x.Text?.Count > 0).First()
                };
                
                return new ExpressionResult
                {
                    Return = true,
                    Results = singleValueResult
                };
            }

            var deDupedResults = FilterDownResults(results, request.label);
            returnList.AddRange(deDupedResults);
            
            return new ExpressionResult
            {
                Break = true,
                ContinuePartialLoop = true,
                Results = returnList
            };
        }                        
                        
        returnList.AddRange(results.Where(result => result.MatchedPosition != MatchedPosition.NotFound));

        // NOTE - It may first appear we can do the following - but we need to keep looking because
        // of the way we look up labels per page
        /*if (label.MultipleBehaviour is MultipleType.FindSingleInstanceOfLabelWithMultipleValues)
        {
            label.Completed = true;
            return returnList;
        }*/
        
        /*var askedToLookForMore = ifMultiplePreferLast || ifMultiplePreferLongest;
        
        if (matchedLabel.MultipleBehaviour is MultipleType.FindSingleInstanceOfLabelWithMultipleValues
            && !askedToLookForMore)
        {
            return returnList;
        }*/

        if (request.label!.Position == LabelPosition.TextToFindIsBetweenLabels
            && results.Count > 0)
        {
            var result = results[0];

            if (result.LabelStartLineNumber == partialLine.LineNumber)
            {
                var resultText = result.Text?.FirstOrDefault()?.Text;

                if (resultText != null)
                {
                    var startIndexOfMatch =
                        partialLine.Text.IndexOf(resultText,
                            StringComparison.OrdinalIgnoreCase);

                    var endIndexOfMatch = startIndexOfMatch + resultText.Length;

                    if (startIndexOfMatch > -1 && partialLine.Text.Length > endIndexOfMatch)
                    {
                        var newPartialLineText = partialLine.Text[endIndexOfMatch..];

                        if (newPartialLineText != string.Empty)
                        {
                            var newPartialLineTextWords = partialLine.Columns
                                .SelectMany(c => c.Words)
                                .ToList();
                            
                            newPartialLineTextWords = DocumentLineColumn.FilterWordsFromText(newPartialLineTextWords, newPartialLineText);
                            
                            partialLine = partialLine.Clone();
                            partialLine.Columns.Clear();
                            partialLine.Columns.Add(new DocumentLineColumn(newPartialLineTextWords));

                            newPartialLine = partialLine;
                            continuePartialLoop = true;
                        }
                    }
                }
            }
        }

        return new ExpressionResult
        {
            ContinuePartialLoop = continuePartialLoop,
            NewPartialLine = newPartialLine,
            Results = returnList
        };
    }
}