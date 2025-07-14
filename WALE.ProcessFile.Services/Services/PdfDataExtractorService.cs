using System.Text.Json;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Services;

public partial class PdfDataExtractorService(
    INoOcrDataExtractorService noOcrDataExtractorService,
    IEnumerable<IOcrDataExtractorService> ocrDataExtractorServices,
    string pdfFolderPath)
    : IPdfDataExtractorService
{
    public bool InUse { get; set; } = false;
    
    private const int UNKNOWN_LINES_TOTAL = -1;
    private const int POSITION_NOT_FOUND = -1;
    
    public async Task<IReadOnlyList<LabelGroupResult>> GetMatchesAsync(
        string pdfFilePath,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache)
    {
        var pdfDocument = await noOcrDataExtractorService.GetPdfDocumentAsync(
            pdfFilePath,
            GetFileOutputFolder(outputFolder, pdfFilePath),
            useCache);
        
        // Save screenshots
        if (!pdfDocument.FromCache)
        {
            var saveTasks = pdfDocument.Pages
                .Select(page => noOcrDataExtractorService.SavePageScreenshotAsync(pdfDocument, page.Number));

            await Task.WhenAll(saveTasks);
        }
        
        var documentLines =
            await noOcrDataExtractorService.GetTextLinesFromPdfAsync(pdfDocument);
        
        // Save all text
        if (!pdfDocument.FromCache)
        {
            var folder = $"{pdfDocument.OutputFolder}/{noOcrDataExtractorService.Name}/Text";
            Directory.CreateDirectory(folder);
            
            await File.WriteAllTextAsync(
                $"{folder}/pages-all.txt",
                string.Join("\r\n", documentLines
                    .Select(line => $"{line.LineNumber} {line.Text}")
                    .ToArray()));
        }

        const bool notOcr = false;
        
        var labelGroupMatches = await GetLabelGroupMatchesAsync(
            documentLines,
            labelLookups,
            notOcr,
            noOcrDataExtractorService.Name,
            licenceMapping,
            previouslyParsedPaths,
            outputFolder,
            useCache);

        var unmatchedLabelLookups = labelLookups
            .Where(labelLookup =>
                labelGroupMatches.All(labelGroupResult =>
                    labelGroupResult.LabelGroupName != labelLookup.LabelGroupName))
            .ToList();

        if (unmatchedLabelLookups.Count == 0)
        {
            return labelGroupMatches;
        }
        
        documentLines = [];

        var metadataFolder = $"{pdfDocument.OutputFolder}/{noOcrDataExtractorService.Name}/Images";
        Directory.CreateDirectory(metadataFolder); // This checks if exists, and creates the whole path too
        var metadataFilename = $"{metadataFolder}/metadata.json";
        
        if (pdfDocument.FromCache && File.Exists(metadataFilename))
        {
            var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
            var metadata = JsonSerializer.Deserialize<Metadata>(metaDataFileText);

            var pageNumber = 0;
            
            foreach (var page in metadata!.Pages)
            {
                pageNumber += 1;
                var imageNumber = 1;

                foreach (var loopImageNumber in page.ImageNumbers)
                {
                    var thisImageNumber = imageNumber++;

                    foreach (var ocrService in ocrDataExtractorServices
                        .OrderBy(service => service.HasDirectCost))
                    {
                        var imageLines =
                            await ocrService.GetTextLinesFromImageAsync(
                                [], // TODO - diff overload without this?
                                pageNumber,
                                loopImageNumber,
                                pdfDocument);
                        
                        var tempLines = documentLines.ToList();
                        tempLines.AddRange(imageLines);

                        const bool isOcr = true;
                        
                        var ocrResult = await GetLabelGroupMatchesAsync(
                            tempLines,
                            unmatchedLabelLookups,
                            isOcr,
                            ocrService.Name,
                            licenceMapping,
                            previouslyParsedPaths,
                            outputFolder,
                            useCache);
                        
                        if (ocrResult.Count == 0)
                        {
                            if (ocrDataExtractorServices.Count() == ++imageNumber)
                            {
                                documentLines.AddRange(imageLines);
                            }

                            continue;
                        }

                        labelGroupMatches.AddRange(ocrResult);
                        unmatchedLabelLookups = unmatchedLabelLookups
                            .Where(labelLookup =>
                                labelGroupMatches.All(r => r.LabelGroupName != labelLookup.LabelGroupName))
                            .ToList();

                        if (unmatchedLabelLookups.Count == 0)
                        {
                            break;
                        }                        
                    }
                }
                
                unmatchedLabelLookups = unmatchedLabelLookups
                    .Where(labelLookup =>
                        labelGroupMatches.All(r => r.LabelGroupName != labelLookup.LabelGroupName))
                    .ToList();

                if (unmatchedLabelLookups.Count == 0)
                {
                    break;
                }                
            }
        }
        else
        {
            var metadata = new Metadata();
            
            var pagesWithImages = await noOcrDataExtractorService
                .GetPagesThatContainImagesAsync(pdfDocument, pdfFilePath);

            // TODO - This OCR solution doesnt yet support 'Between' going over multiple pages
            foreach (var page in pagesWithImages)
            {
                var metadataPage = new MetadataPage();
                metadata.Pages.Add(metadataPage);
                
                var imageNumber = 1;

                foreach (var image in await page.GetImagesAsync())
                {
                    var thisImageNumber = imageNumber++;
                    metadataPage.ImageNumbers.Add(thisImageNumber);
                    
                    foreach (var ocrService in ocrDataExtractorServices
                        .OrderBy(service => service.HasDirectCost))
                    {
                        var imageBytes = await image.GetImageBytesAsync(
                            thisImageNumber,
                            page.Number,
                            pdfDocument.OutputFolder);

                        var imageLines =
                            await ocrService.GetTextLinesFromImageAsync(
                                imageBytes,
                                page.Number,
                                thisImageNumber,
                                pdfDocument);

                        Directory.CreateDirectory($"{pdfDocument.OutputFolder}/{ocrService.Name}/Text");
                        
                        await File.WriteAllTextAsync(
                            $"{pdfDocument.OutputFolder}/{ocrService.Name}/Text/page-{page.Number}-image-{thisImageNumber}.txt",
                            string.Join("\r\n", imageLines.Select(x => $"{x.LineNumber} {x.Text}").ToArray()));

                        var tempLines = documentLines.ToList();
                        tempLines.AddRange(imageLines);

                        var ocrResult = await GetLabelGroupMatchesAsync(
                            tempLines,
                            unmatchedLabelLookups,
                            true,
                            ocrService.Name,
                            licenceMapping,
                            previouslyParsedPaths,
                            outputFolder,
                            useCache);

                        if (ocrResult.Count == 0)
                        {
                            if (ocrDataExtractorServices.Count() == ++imageNumber)
                            {
                                documentLines.AddRange(imageLines);
                            }

                            continue;
                        }

                        labelGroupMatches.AddRange(ocrResult);
                        unmatchedLabelLookups = unmatchedLabelLookups
                            .Where(labelLookup =>
                                labelGroupMatches.All(r => r.LabelGroupName != labelLookup.LabelGroupName))
                            .ToList();

                        if (unmatchedLabelLookups.Count == 0)
                        {
                            break;
                        }
                    }
                }

                unmatchedLabelLookups = unmatchedLabelLookups
                    .Where(labelLookup =>
                        labelGroupMatches.All(r => r.LabelGroupName != labelLookup.LabelGroupName))
                    .ToList();

                if (unmatchedLabelLookups.Count == 0)
                {
                    break;
                }
            }
            
            await File.WriteAllTextAsync(
                metadataFilename,
                JsonSerializer.Serialize(metadata));
        }

        noOcrDataExtractorService.Release(pdfDocument);
        return labelGroupMatches;
    }

    private async Task<List<LabelGroupResult>> GetLabelGroupMatchesAsync(
        IReadOnlyList<DocumentLine> documentLines,
        IEnumerable<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        bool isOcr,
        string serviceName,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache)
    {
        var labelGroupMatches = new List<LabelGroupResult>();

        if (documentLines.Count == 0)
        {
            return labelGroupMatches;
        }
        
        foreach (var (labelGroupName, labels) in labelLookups)
        {
            if (AlreadyMatchedLabelGroup(labelGroupMatches, labelGroupName))
            {
                continue;
            }
            
            foreach (var label in labels)
            {
                if (!LabelIsInDocument(label, documentLines))
                {
                    continue;
                }

                var labelGroupMatch = await FindLabelGroupMatchesInLinesAsync(
                    GetLines(documentLines, label),
                    labels,
                    isOcr,
                    serviceName,
                    labelGroupName,
                    labelGroupMatches,
                    licenceMapping,
                    previouslyParsedPaths,
                    outputFolder,
                    useCache);

                if (labelGroupMatch.Count == 0)
                {
                    continue;
                }

                foreach (var labelGroup in labelGroupMatch)
                {
                    labelGroup.LabelGroupName = labelGroupName;    
                }
                
                labelGroupMatches.AddRange(labelGroupMatch);
                break;
            }
        }

        return labelGroupMatches;
    }
    
    private class Metadata
    {
        public List<MetadataPage> Pages { get; set; } = [];
    }

    private class MetadataPage
    {
        public List<int> ImageNumbers { get; set; } = [];
    }
    
    private static string GetFileOutputFolder(string outputFolder, string pdfFilePath)
    {
        var fileOutputFolder = Path.Combine(outputFolder, GetFilenameWithoutExtensions(pdfFilePath));
        if (fileOutputFolder.StartsWith('/'))
        {
            fileOutputFolder = fileOutputFolder[1..];
        }

        return fileOutputFolder;
    }

    private static bool AlreadyMatchedLabelGroup(
        IEnumerable<LabelGroupResult> returnList,
        string type)
    {
        return returnList.Any(returnItem => returnItem.LabelGroupName == type);
    }
    
    private static List<DocumentLine>? GetTextBetween(
        IReadOnlyList<string> textEnd,
        IReadOnlyList<string>? containsText,
        string? firstLineTextAfterLabel,
        IReadOnlyList<DocumentLine> nextLines,
        int startLineNumber,
        DocumentLine lineInput,
        out (string matchedEndText, string matchedContainsText)? matchData)
    {
        matchData = null;
        var foundEndTag = false;
        
        var lineCount = 0;
        var returnList = new List<DocumentLine>();
        
        if (!string.IsNullOrEmpty(firstLineTextAfterLabel))
        {
            returnList.Add(new DocumentLine(
                TrimFormatting(firstLineTextAfterLabel)!,
                startLineNumber,
                lineInput.PageNumber,
                lineInput.Words));
        }

        var totalLines = nextLines.Count;
        
        foreach (var line in nextLines)
        {
            var label = new LabelToMatch
            {
                Text = textEnd
            };
            
            if (LineContainsLabel(line, label.Text, label.Position, lineCount++, totalLines, out var matchedEndTextTemp))
            {
                matchData = (matchedEndTextTemp!, "[WILL_BE_REPLACED_LATER]");
                foundEndTag = true;

                break;
            }
            
            var text = TrimFormatting(line.Text)!;
            returnList.Add(new DocumentLine(
                text,
                line.LineNumber,
                line.PageNumber,
                line.Words.ToList()));
        }

        if (!foundEndTag && textEnd.Contains("[END_OF_BLOCK]"))
        {
            matchData = ("[END_OF_BLOCK]", "[WILL_BE_REPLACED_LATER]");            
            foundEndTag = true;
        }

        if (containsText == null)
        {
            return returnList;
        }

        string? matchedContains = null;
        
        var result = foundEndTag && containsText.Any(containsInstance =>
        {
            var matchResult = string.IsNullOrEmpty(containsInstance) || returnList.Any(line =>
                line.Text.Contains(containsInstance, StringComparison.InvariantCultureIgnoreCase));

            if (!matchResult)
            {
                return false;
            }
            
            matchedContains = containsInstance;
            return true;

        }) ? returnList : null;

        if (matchedContains != null)
        {
            matchData = (matchData!.Value.matchedEndText, matchedContains!);
            return result;
        }

        matchData = null;
        return result;
    }
    
    private async Task<IReadOnlyList<LabelGroupResult>> FindLabelGroupMatchesInLinesAsync(
        IReadOnlyList<(DocumentLine Line, IReadOnlyList<DocumentLine> PreviousNLines, IReadOnlyList<DocumentLine> NextNLines)> lines,
        IReadOnlyList<LabelToMatch> labels,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        IReadOnlyList<LabelGroupResult> siblingMatches,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache)
    {
        var returnList = new List<LabelGroupResult>();

        var lineCount = 0;
        var totalLineCount = lines.Count;
        
        foreach (var (line, previousLines, nextLines) in lines)
        {
            var textBeforeAndAfterLabel = new List<(string? Text, LabelToMatch Label)>();
            var matchedLabel = (LabelToMatch?)null;
            
            foreach (var label in labels.Where(whereLabel => !whereLabel.Completed))
            {
                if (label.Format == "LinkedLicence")
                {
                    var licenceNumbers = siblingMatches
                        .Where(siblingMatch => siblingMatch.MatchedLabel?.Name == label.RelatedName)
                        .Select(result => result.Text?.FirstOrDefault())
                        .ToList();
                    
                    var pathsToFetch = new List<string>();
                    
                    foreach (var licenceNumber in licenceNumbers)
                    {
                        if (!string.IsNullOrEmpty(licenceNumber?.Text))
                        {
                            if (!licenceMapping.TryGetValue(licenceNumber.Text, out var relatedFileName))
                            {
                                continue;
                                // TODO ultimately this should throw an error, but silently skip while developing
                            }
                            
                            relatedFileName = $"{pdfFolderPath}{relatedFileName}";
                            
                            if (previouslyParsedPaths.Contains(relatedFileName))
                            {
                                continue;
                            }

                            previouslyParsedPaths.Add(relatedFileName);
                            pathsToFetch.Add(relatedFileName);
                        }
                    }

                    foreach (var relatedFileName in pathsToFetch)
                    {
                        var relatedFileMatches = await GetMatchesAsync(
                            relatedFileName,
                            Configuration.LabelConfiguration.GetLabels(),
                            licenceMapping,
                            previouslyParsedPaths,
                            outputFolder,
                            useCache);

                        var labelResult = new LabelGroupResult
                        {
                            MatchedLabel = label,
                            SubResults = relatedFileMatches,
                            PageNumber = line.PageNumber
                        };
                        
                        RemoveRemoves(labelResult, []); // TODO do this properly at some point
                        returnList.Add(labelResult);
                    }

                    if (pathsToFetch.Count > 0)
                    {
                        label.Completed = true;
                    }

                    continue;
                }

                if (IsLineEmpty(line)
                    && label.Text?.Any(text => text.Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase)) != true
                    && !(label.Position == LabelPosition.Split && lineCount == totalLineCount - 1))
                {
                    continue;
                }
                
                if (label.Text?.Any(x => x.ToLower().Contains("from")) == true)
                {
                    // TODO what was this for?
                }
                
                if (line.Text.Contains("Licensee"))
                {
                    // TODO what was this for?                    
                }

                if (label.Name == "PurposeLink")
                {
                    
                }
                
                if (!LineContainsLabel(line, label.Text, label.Position, lineCount, totalLineCount, out var matchedText))
                {
                    continue;
                }
                
                if (label.Name == "TextWithoutPoints")
                {
                    // TODO what was this for?                    
                }

                if (line.Text.Contains("2025"))
                {
                    // TODO what was this for?                    
                }

                if (label.MatchAllText)
                {
                    var matchedAll = true;
                    
                    foreach (var labelText in label.Text!)
                    {
                        if (LineContainsLabel(line, [labelText], label.Position, lineCount, UNKNOWN_LINES_TOTAL, out _))
                        {
                            continue;
                        }

                        var continueOuterLoop = false;
                        
                        foreach (var previousLine in previousLines)
                        {
                            if (LineContainsLabel(previousLine, [labelText], label.Position, lineCount, UNKNOWN_LINES_TOTAL, out _))
                            {
                                continueOuterLoop = true;
                                break;
                            }
                        }                        
                        
                        foreach (var nextLine in nextLines)
                        {
                            if (LineContainsLabel(nextLine, [labelText], label.Position, lineCount, UNKNOWN_LINES_TOTAL, out _))
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
                        continue;
                    }
                    
                    matchedLabel = label;
                }
                else
                {
                    var clonedLabel = label.Clone();

                    if (matchedText != null)
                    {
                        clonedLabel.Text = [matchedText];
                    }

                    matchedLabel = clonedLabel;
                }
                
                textBeforeAndAfterLabel.AddRange(
                    GetLineBeforeAndAfterText(line, matchedLabel));
                
                break;
            }

            if (matchedLabel == null)
            {
                lineCount += 1;
                continue;
            }

            var lookupExpressions = GetRelevantLookupExpressions(
                isOcr,
                serviceName,
                labelGroupName,
                matchedLabel,
                previousLines,
                nextLines,
                textBeforeAndAfterLabel,
                line.LineNumber,
                siblingMatches,
                line,
                licenceMapping,
                previouslyParsedPaths,
                outputFolder,
                useCache);
            
            foreach (var expression in lookupExpressions)
            {
                var results = await expression();
                if (results.Count == 0)
                {
                    continue;
                }

                returnList.AddRange(results.Where(result => result.MatchType != MatchType.NotFound));

                if (matchedLabel.Multiple == MultipleType.False)
                {
                    return returnList;
                }
            }
            
            // Don't carry on if we've identified it was a succession document
            if (matchedLabel.Position == LabelPosition.ContractIsSuccession)
            {
                break;
            }
            
            lineCount += 1;
        }

        if (returnList.Count > 1 && returnList.All(match =>
            match.MatchedLabel?.Multiple == MultipleType.SingleLabelSingleValueMultipleLines))
        {
            var textList = new List<DocumentLine>();

            foreach (var returnListLoop in returnList)
            {
                textList.AddRange(returnListLoop.Text!);
            }
            
            var returnItem = returnList.First();
            
            return new List<LabelGroupResult>
            {
                new()
                {
                    MatchedLabel = returnItem.MatchedLabel!.Clone(),
                    LabelGroupName = returnItem.LabelGroupName,
                    MatchType = returnItem.MatchType,
                    PageNumber = returnItem.PageNumber,
                    ServiceName = returnItem.ServiceName,
                    Text = textList
                }
            };
        }
        
        return returnList;
    }
    
    private IEnumerable<Func<Task<List<LabelGroupResult>>>> GetRelevantLookupExpressions(
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        LabelToMatch label,
        IReadOnlyList<DocumentLine> previousLines,
        IReadOnlyList<DocumentLine> nextLines,
        List<(string? Text, LabelToMatch Label)> textBeforeAndAfterLabel,
        int lineNumber, // IN THEORY CAN GET RID OF THIS AS ITS REDUNDANT
        IReadOnlyList<LabelGroupResult> siblingMatches,
        DocumentLine line,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache)
    {
        var isCompanyType = label.Format == "CompanyName";
        var isNumberLookup = label.Format == "Number";
        var isUnitsLookup = label.Format == "Units";
        var isSingleWord = label.Format == "SingleWord";
        var actsLikeSingleWord = label.Format == "ActsLikeSingleWord";
        var isLicenceNumberLookup = label.Format == "LicenceNumber";
        var isDateOrPurposeLookup = label.Format == "DateOrPurpose";
        
        var labelGroupResult = new LabelGroupResult
        {
            IsOcr = isOcr,
            LineNumber = lineNumber,
            PageNumber = line.PageNumber,
            ServiceName = serviceName
        };
        
        var expressions = new List<(LabelPosition Position, Func<Task<List<LabelGroupResult>>> ResultIfMatched, int Order)>
        {
            (LabelPosition.ApplicableToAll,
                async () =>
                {
                    if (label?.Name == "PointPointNumber")
                    {
                        
                    }
                    
                    labelGroupResult = labelGroupResult.Clone();
                    var returnListTop = new List<LabelGroupResult>();
                    
                    if (!PotentialMatchOnLabelLine(textBeforeAndAfterLabel))
                    {
                        return returnListTop;
                    }
                    
                    foreach (var (text, matchedLabel) in textBeforeAndAfterLabel)
                    {
                        var t = matchedLabel.IncludeLabelText ? line.Text : text;
                        
                        var over2Lines = false;
                        var outputText = RemoveExcludes(matchedLabel, t!, out var removedLines);

                        if (IsCorruptedText(outputText))
                        {
                            continue;
                        }

                        if (isDateOrPurposeLookup && IsDateOrPurpose(t))
                        {
                            labelGroupResult.Text =
                            [
                                new DocumentLine(
                                    t!,
                                    line.LineNumber,
                                    line.PageNumber,
                                    line.Words.ToList())
                            ];
                            
                            labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                            labelGroupResult.MatchedLabel = matchedLabel;
                            RemoveRemoves(labelGroupResult, removedLines);

                            return [labelGroupResult];
                        }
                        
                        if (isCompanyType
                            && (char.IsLower(outputText[0])
                                || outputText.StartsWith("trading as", StringComparison.InvariantCultureIgnoreCase)))
                        {
                            over2Lines = true;
                            outputText = $"{previousLines.FirstOrDefault()?.Text} {outputText}";
                        }

                        if (isNumberLookup
                            && TryGetNumber(outputText, line.LineNumber, line.PageNumber, out var numberLine))
                        {
                            numberLine!.Words = line.Words;
                            
                            labelGroupResult.Text = [numberLine];
                            labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                            labelGroupResult.MatchedLabel = matchedLabel;
                            RemoveRemoves(labelGroupResult, removedLines);

                            return [labelGroupResult];
                        }
                        
                        if (isLicenceNumberLookup
                            && label != null
                            && AnyIsLicenceNumber([
                                new DocumentLine(
                                    outputText,
                                    lineNumber,
                                    line.PageNumber,
                                    line.Words.ToList())],
                                label,
                                out var licenceNumberLines))
                        {
                            var returnList = new List<LabelGroupResult>();
                            
                            foreach (var licenceNumberLine in licenceNumberLines)
                            {
                                labelGroupResult = labelGroupResult.Clone();
                                labelGroupResult.Text = [licenceNumberLine];
                                labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                                labelGroupResult.MatchedLabel = matchedLabel;
                                RemoveRemoves(labelGroupResult, removedLines);
                                
                                returnList.Add(labelGroupResult);
                            }

                            return returnList;
                        }

                        if ((isSingleWord || actsLikeSingleWord) && label != null && !string.IsNullOrEmpty(t))
                        {
                            labelGroupResult.Text =
                            [
                                new DocumentLine(
                                    isSingleWord ? t.Split(' ')[0] : t,
                                    line.LineNumber,
                                    line.PageNumber,
                                    line.Words.ToList())
                            ];
                            
                            labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                            labelGroupResult.MatchedLabel = matchedLabel;
                            RemoveRemoves(labelGroupResult, removedLines);

                            var results = new List<LabelGroupResult> {labelGroupResult};
                            
                            foreach (var result in results)
                            {
                                var subResults = await ProcessSubLabelsAsync(
                                    label,
                                    result.Text!,
                                    isOcr,
                                    serviceName,
                                    labelGroupName,
                                    licenceMapping,
                                    previouslyParsedPaths,
                                    outputFolder,
                                    useCache);
                    
                                if (label.MinimumSubMatches.HasValue && label.MinimumSubMatches.Value > subResults.Count)
                                {
                                    return [];
                                }

                                result.SubResults = subResults;
                            }
                            
                            return results;
                        }

                        var isPossiblity = false;
                        
                        if (matchedLabel.Possibilities?.Any() == true)
                        {
                            var autoCorrectedOutputText = AutoCorrectText(
                                new DocumentLine(
                                    outputText,
                                    lineNumber,
                                    line.PageNumber,
                                    line.Words.ToList()),
                                false);
                            
                            foreach (var possibility in matchedLabel.Possibilities)
                            {
                                if (!outputText.Contains(possibility, StringComparison.InvariantCultureIgnoreCase)
                                    && !autoCorrectedOutputText!.Contains(possibility, StringComparison.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }
                                
                                outputText = possibility;
                                isPossiblity = true;
                                
                                break;
                            }
                        }
                        
                        if (isUnitsLookup)
                        {
                            if (matchedLabel.Possibilities == null)
                            {
                                continue;
                            }
                            
                            foreach (var possibility in matchedLabel.Possibilities!)
                            {
                                if (!outputText.Contains(possibility,
                                    StringComparison.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }

                                labelGroupResult.Text =
                                [
                                    new DocumentLine(
                                        possibility,
                                        line.LineNumber,
                                        line.PageNumber,
                                        line.Words.ToList())
                                ];
                                
                                labelGroupResult.MatchType = MatchType.SameLineIsCompany1Line;
                                labelGroupResult.MatchedLabel = matchedLabel;
                                RemoveRemoves(labelGroupResult, removedLines);
                                labelGroupResult.MatchedLabel.Possibilities = [possibility];
                                
                                return new List<LabelGroupResult> { labelGroupResult };
                            }
                        }

                        outputText = TrimFormatting(outputText);
                        outputText = isOcr ? AutoCorrectText(new DocumentLine(
                            outputText!,
                            lineNumber,
                            line.PageNumber,
                            line.Words.ToList()), isCompanyType) : outputText;

                        if (isCompanyType
                            && TryGetCompanyOrPersonalName(new DocumentLine(
                                outputText!,
                                lineNumber,
                                line.PageNumber,
                                line.Words.ToList()), out _))
                        {
                            var matchType = over2Lines ?
                                MatchType.SameLineIsCompany2Lines
                                : MatchType.SameLineIsCompany1Line;
                            
                            labelGroupResult.Text =
                            [
                                new DocumentLine(
                                    outputText!,
                                    line.LineNumber,
                                    line.PageNumber,
                                    line.Words.ToList())
                            ];
                            
                            labelGroupResult.MatchType = matchType;
                            labelGroupResult.MatchedLabel = matchedLabel;
                            RemoveRemoves(labelGroupResult, removedLines);

                            if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                            {
                                labelGroupResult.MatchedLabel.Possibilities = [outputText!];   
                            }
                            
                            return [labelGroupResult];
                        }

                        var trimmedSplit = outputText!.Trim().Split(' ');

                        if (trimmedSplit.Length == 1
                            && !string.IsNullOrEmpty(trimmedSplit[0])
                            && isCompanyType)
                        {
                            labelGroupResult.Text =
                            [
                                new DocumentLine(
                                    outputText,
                                    lineNumber,
                                    line.PageNumber,
                                    line.Words.ToList())
                            ];
                            
                            labelGroupResult.MatchType = MatchType.SameLineSingleWord;
                            labelGroupResult.MatchedLabel = matchedLabel;
                            RemoveRemoves(labelGroupResult, removedLines);
                            
                            if (labelGroupResult.MatchedLabel.Possibilities != null && isPossiblity)
                            {
                                labelGroupResult.MatchedLabel.Possibilities = [outputText];   
                            }
                            
                            return [labelGroupResult];
                        }

                        if (label?.Text == null && !string.IsNullOrWhiteSpace(outputText))
                        {
                            var lineMatch = labelGroupResult.Clone();
                            lineMatch.Text =
                            [
                                new DocumentLine(
                                    outputText,
                                    line.LineNumber,
                                    line.PageNumber,
                                    line.Words.ToList())
                            ];
                            
                            lineMatch.MatchType = MatchType.Between;
                            lineMatch.MatchedLabel = label;
                            RemoveRemoves(lineMatch, removedLines);
                            
                            returnListTop.Add(lineMatch);
                        }
                    }

                    return returnListTop;
                }, 0),
            (LabelPosition.Split,
                async () =>
                {
                    if (label.Name == "PurposeLinkSub")
                    {
                        
                    }
                    
                    if (label.Text == null || label.Text.Count == 0)
                    {
                        throw new Exception("Incorrect configuration - if position is Split, Text must be set");
                    }

                    var lineContainsLabel = LineContainsLabel(
                        line,
                        label.Text,
                        LabelPosition.Split,
                        UNKNOWN_LINES_TOTAL,
                        int.MaxValue,
                        out _);

                    var sub1Result = labelGroupResult.Clone();
                    var sub1ResultText  = previousLines.Reverse().ToList();

                    if (!lineContainsLabel)
                    {
                        sub1ResultText.Add(line);
                    }
                    
                    var sub2Result = labelGroupResult.Clone();
                    var sub2ResultText = nextLines.ToList();

                    if (lineContainsLabel)
                    {
                        if (sub1ResultText.Count == 0 && sub2ResultText.Count == 0)
                        {
                            var splitter = string.Join(' ', label.Text);
                            var parts = line.Text.Split(splitter);

                            parts[0] = parts[0].Trim();
                            parts[1] = parts[1].Trim();
                            
                            var lineWords1 = parts[0]
                                .Split(' ')
                                .Select(t => new DocumentLineWord(t, null, []))
                                .ToList();

                            var lineWords2 = parts[1]
                                .Split(' ')
                                .Select(t => new DocumentLineWord(t, null, []))
                                .ToList();                                
                            
                            sub1ResultText = [new DocumentLine(parts[0], lineNumber, line.PageNumber, lineWords1)];
                            sub2ResultText = [new DocumentLine(parts[1], lineNumber, line.PageNumber, lineWords2)];
                        }
                        else
                        {
                            sub2ResultText.Insert(0, line);

                            if (sub1ResultText.Count > 0)
                            {
                                var lastLine = sub1ResultText.Last();
                                sub2ResultText.Insert(0, lastLine);

                                sub1ResultText.Remove(lastLine);
                            }
                        }
                    }

                    sub1ResultText = RemoveMultipleBlankLines(sub1ResultText);
                    sub2ResultText = RemoveMultipleBlankLines(sub2ResultText);
                    
                    sub1Result.Text = sub1ResultText;
                    sub2Result.Text = sub2ResultText;

                    var results = new List<LabelGroupResult>
                    {
                        sub1Result
                    };

                    if (sub2Result.Text.Count > 0)
                    {
                        results.Add(sub2Result);
                    }

                    foreach (var result in results)
                    {
                        var subResults = await ProcessSubLabelsAsync(
                            label,
                            result.Text!,
                            isOcr,
                            serviceName,
                            labelGroupName,
                            licenceMapping,
                            previouslyParsedPaths,
                            outputFolder,
                            useCache);
                    
                        if (label.MinimumSubMatches.HasValue && label.MinimumSubMatches.Value > subResults.Count)
                        {
                            return [];
                        }

                        result.SubResults = subResults;
                    }
                    
                    return results;
                }
            , 0
            ),
            (LabelPosition.RelatedCategoryPosition,
                async () =>
                {
                    labelGroupResult = labelGroupResult.Clone();
                    
                    var categoryItems = siblingMatches
                        .Where(z => z.MatchedLabel!.CategoryName == label.RelatedCategoryName)
                        .OrderBy(z => z.LineNumber)
                        .ToList();

                    int matchedLabelLineNumber = -1;
                    
                    foreach (var categoryItem in categoryItems)
                    {
                        if (categoryItem.MatchedLabel?.Name == label.RelatedName)
                        {
                            matchedLabelLineNumber = categoryItem.LineNumber;
                            break;
                        }
                    }

                    var matches = new List<DocumentLine>();

                    foreach (var previousLine in previousLines.OrderByDescending(x => x.LineNumber))
                    {
                        if (AnyIsNumber([previousLine], out var numberLine))
                        {
                            matches.Add(numberLine!);
                        }
                    }
                    
                    foreach (var nextLine in nextLines.OrderBy(x => x.LineNumber))
                    {
                        if (AnyIsNumber([nextLine], out var numberLine))
                        {
                            matches.Add(numberLine!);
                        }
                    }

                    var xmatches = matches
                        .OrderBy(x => Math.Abs(matchedLabelLineNumber - x.LineNumber))
                        .ThenBy(x => x.LineNumber)
                        .ToList();
                    
                    if (xmatches.Count > 0)
                    {
                        labelGroupResult.Text = [
                            new DocumentLine(
                                xmatches.FirstOrDefault()?.Text!,
                                -1,
                                -1,
                                [])
                        ];
                        
                        labelGroupResult.MatchedLabel = label;

                        // TODO should set match type
                        RemoveRemoves(labelGroupResult, []); // TODO probably do something else
                        
                        return [labelGroupResult];
                    }
                    
                    return [];
                }
            , 0),
            (LabelPosition.TextToFindIsBetweenLabels,
                async () =>
                {
                    labelGroupResult = labelGroupResult.Clone();
                    var linesToUse = new List<DocumentLine>();/*
                    {
                        line
                    };*/

                    if (label.Name == "Purpose")
                    {
                        
                    }

                    if (label.LeewayBefore >= 1 && previousLines.Count >= label.LeewayBefore) // TODO never currently set
                    {
                        linesToUse.Add(previousLines[^label.LeewayBefore]);
                    }

                    if (label.Text?.Any(t => line.Text.Contains(t, StringComparison.InvariantCultureIgnoreCase)) != true)
                    {
                        linesToUse.Add(line);                        
                    }
                    
                    linesToUse.AddRange(nextLines);
                    
                    var betweenText = GetTextBetween(
                        label.TextEnd!,
                        label.MustContain,
                        textBeforeAndAfterLabel.LastOrDefault(
                            tuple => tuple.Label.Position is LabelPosition.LabelIsBeforeTextToFind
                                or LabelPosition.TextToFindIsBetweenLabels).Text,
                        linesToUse,
                        lineNumber,
                        line,
                        out var matchedEndText);
                    
                    if (betweenText == null)
                    {
                        return [];
                    }

                    if (label.IncludeLabelText && betweenText.Count >= 1)
                    {
                        betweenText[0] = line;
                    }
                    
                    betweenText = betweenText
                        .Where(betweenLine => !IsCorruptedText(betweenLine.Text))
                        .ToList();

                    var subResults = await ProcessSubLabelsAsync(
                        label,
                        betweenText,
                        isOcr,
                        serviceName,
                        labelGroupName,
                        licenceMapping,
                        previouslyParsedPaths,
                        outputFolder,
                        useCache);
                    
                    if (label.MinimumSubMatches.HasValue && label.MinimumSubMatches.Value > subResults.Count)
                    {
                        return [];
                    }
                    
                    betweenText = RemoveExcludes(label, betweenText, out var removedLines);
                    
                    labelGroupResult.Text = betweenText.ToList();
                    labelGroupResult.MatchType = MatchType.Between;
                    labelGroupResult.MatchedLabel = label.Clone();
                    labelGroupResult.MatchedLabel.TextEnd =
                        [
                            labelGroupResult.MatchedLabel.TextEnd!.Single(x => matchedEndText != null && x == matchedEndText.Value.matchedEndText)
                        ];

                    if (labelGroupResult.MatchedLabel.MustContain != null)
                    {
                        labelGroupResult.MatchedLabel.MustContain =
                        [
                            labelGroupResult.MatchedLabel.MustContain!.Single(x =>
                                matchedEndText != null && x == matchedEndText.Value.matchedContainsText)
                        ];
                    }

                    RemoveRemoves(labelGroupResult, removedLines);
                    labelGroupResult.SubResults = subResults;

                    return [labelGroupResult];
                }, 0),
            (LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore,
                async () =>
                {
                    labelGroupResult = labelGroupResult.Clone();
                    labelGroupResult.MatchType = MatchType.NearNextLineIsCompany;
                    labelGroupResult.MatchedLabel = label.Clone();
                    labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsBeforeTextToFind;

                    var inputLines = previousLines.ToList();
                    inputLines.AddRange(nextLines);
                    
                    var modifiedLines = RemoveExcludes(label, inputLines, out var removedLines);
                    
                    if (isDateOrPurposeLookup && AnyIsDateOrPurpose(inputLines, out var matchedLines))
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

                        return returnList;
                    }
                    
                    if (isCompanyType && AnyIsCompanyOrPersonalName(modifiedLines, false, isOcr, out var companyNameLines))
                    {
                        labelGroupResult.Text = companyNameLines;
                        labelGroupResult.MatchedLabel.Format = "CompanyName";
                        RemoveRemoves(labelGroupResult, removedLines);
                        
                        return [labelGroupResult];
                    }
                    
                    if (isNumberLookup && AnyIsNumber(modifiedLines, out var numberLine))
                    {
                        labelGroupResult.Text = new[] { numberLine! };
                        labelGroupResult.MatchedLabel.Format = "Number";
                        RemoveRemoves(labelGroupResult, removedLines);
                        
                        return [labelGroupResult];
                    }

                    if (isLicenceNumberLookup && AnyIsLicenceNumber(modifiedLines, label, out var licenceNumberLines))
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

                        return returnList;
                    }
                    
                    if (isSingleWord && modifiedLines.FirstOrDefault() != null)
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
                            
                        return [labelGroupResult];
                    }
                    
                    if (isUnitsLookup)
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

                                labelGroupResult.Text = new[]
                                {
                                    new DocumentLine(
                                        possibility,
                                        nextLine.LineNumber,
                                        nextLine.PageNumber,
                                        nextLine.Words.ToList())
                                };
                                
                                labelGroupResult.MatchedLabel.Format = "Units";
                                RemoveRemoves(labelGroupResult, removedLines);
                                labelGroupResult.MatchedLabel.Possibilities = [possibility];
                                
                                return [labelGroupResult];
                            }
                        }
                    }
                    
                    return [];
                },
                -1),
            (LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter,
                async () =>
                {
                    labelGroupResult = labelGroupResult.Clone();
                    labelGroupResult.MatchType = MatchType.NearPreviousLineIsCompany;
                    labelGroupResult.MatchedLabel = label.Clone();
                    labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
                    
                    var inputLines = previousLines.ToList();
                    inputLines.Reverse();
                    inputLines.AddRange(nextLines);
                    
                    var modifiedLines = RemoveExcludes(label, inputLines, out var removedLines);
                    
                    if (isDateOrPurposeLookup && AnyIsDateOrPurpose(previousLines, out var matchedLines))
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

                        return returnList;
                    }
                    
                    if (isCompanyType && AnyIsCompanyOrPersonalName(modifiedLines, false, isOcr, out var companyNameLines))
                    {
                        labelGroupResult.Text = companyNameLines;
                        labelGroupResult.MatchedLabel.Format = "CompanyName";
                        RemoveRemoves(labelGroupResult, removedLines);
                        
                        labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
                        
                        return [labelGroupResult];
                    }

                    if (isNumberLookup && AnyIsNumber(modifiedLines, out var numberLine))
                    {
                        labelGroupResult.Text = new[] { numberLine! };
                        labelGroupResult.MatchedLabel.Format = "Number";
                        RemoveRemoves(labelGroupResult, removedLines);                        
                        
                        return [labelGroupResult];
                    }
                    
                    if (isLicenceNumberLookup && AnyIsLicenceNumber(modifiedLines, label, out var licenceNumberLines))
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

                        return returnList;
                    }
                    
                    if (isUnitsLookup)
                    {
                        foreach (var previousLine in modifiedLines)
                        {
                            if (labelGroupResult.MatchedLabel.Possibilities == null)
                            {
                                continue;
                            }
                            
                            foreach (var possibility in labelGroupResult.MatchedLabel.Possibilities!)
                            {
                                if (!previousLine.Text.Contains(possibility,
                                        StringComparison.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }

                                labelGroupResult.Text = new[] { new DocumentLine(
                                    possibility,
                                    previousLine.LineNumber,
                                    previousLine.PageNumber,
                                    previousLine.Words.ToList()) };
                                labelGroupResult.MatchedLabel.Format = "Units";
                                RemoveRemoves(labelGroupResult, removedLines);
                                labelGroupResult.MatchedLabel.Possibilities = [possibility];
                                
                                return [labelGroupResult];
                            }
                        }
                    }
                    
                    return [];
                },
                -1),
            (LabelPosition.LabelIsBeforeTextToFind,
                async () =>
                {
                    labelGroupResult = labelGroupResult.Clone();
                    labelGroupResult.MatchType = MatchType.NearNextLineIsCompany;
                    labelGroupResult.MatchedLabel = label.Clone();
                    labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsBeforeTextToFind;

                    var modifiedNextLines = RemoveExcludes(label, nextLines, out var removedLines);
                    
                    if (isDateOrPurposeLookup && AnyIsDateOrPurpose(nextLines, out var matchedLines))
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

                        return returnList;
                    }
                    
                    if (isCompanyType && AnyIsCompanyOrPersonalName(modifiedNextLines, false, isOcr, out var companyNameLine))
                    {
                        labelGroupResult.Text = companyNameLine;
                        labelGroupResult.MatchedLabel.Format = "CompanyName";
                        RemoveRemoves(labelGroupResult, removedLines);
                        
                        return [labelGroupResult];
                    }
                    
                    if (isNumberLookup && AnyIsNumber(modifiedNextLines, out var numberLine))
                    {
                        labelGroupResult.Text = new[] { numberLine! };
                        labelGroupResult.MatchedLabel.Format = "Number";
                        RemoveRemoves(labelGroupResult, removedLines);
                        
                        return [labelGroupResult];
                    }

                    if (isLicenceNumberLookup && AnyIsLicenceNumber(modifiedNextLines, label, out var licenceNumberLines))
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

                        return returnList;
                    }
                    
                    if (isSingleWord && modifiedNextLines.FirstOrDefault() != null)
                    {
                        labelGroupResult.Text =
                        [
                            new DocumentLine(
                            modifiedNextLines.First().Text.Split(' ')[0],
                            modifiedNextLines.First().LineNumber,
                            modifiedNextLines.First().PageNumber,
                            modifiedNextLines.First().Words.ToList())
                        ];
                        
                        labelGroupResult.MatchedLabel.Format = "SingleWord";
                        RemoveRemoves(labelGroupResult, removedLines);
                            
                        return [labelGroupResult];
                    }
                    
                    if (isUnitsLookup)
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

                                labelGroupResult.Text = new[]
                                {
                                    new DocumentLine(
                                        possibility,
                                        nextLine.LineNumber,
                                        nextLine.PageNumber,
                                        nextLine.Words.ToList())
                                };
                                
                                labelGroupResult.MatchedLabel.Format = "Units";
                                RemoveRemoves(labelGroupResult, removedLines);
                                labelGroupResult.MatchedLabel.Possibilities = [possibility];
                                
                                return [labelGroupResult];
                            }
                        }
                    }
                    
                    return [];
                },
                0),
            (LabelPosition.LabelIsAfterTextToFind,
                async () =>
                {
                    labelGroupResult = labelGroupResult.Clone();
                    labelGroupResult.MatchType = MatchType.NearPreviousLineIsCompany;
                    labelGroupResult.MatchedLabel = label.Clone();
                    labelGroupResult.MatchedLabel.Position = LabelPosition.LabelIsAfterTextToFind;
                    
                    var modifiedPreviousLines = RemoveExcludes(label, previousLines, out var removedLines);
                    
                    if (isDateOrPurposeLookup && AnyIsDateOrPurpose(previousLines, out var matchedLines))
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

                        return returnList;
                    }
                    
                    if (isCompanyType && AnyIsCompanyOrPersonalName(modifiedPreviousLines, true, isOcr, out var companyNameLine))
                    {
                        labelGroupResult.Text = companyNameLine;
                        labelGroupResult.MatchedLabel.Format = "CompanyName";
                        RemoveRemoves(labelGroupResult, removedLines);
                        
                        return [labelGroupResult];
                    }

                    if (isNumberLookup && AnyIsNumber(modifiedPreviousLines, out var numberLine))
                    {
                        labelGroupResult.Text = new[] { numberLine! };
                        labelGroupResult.MatchedLabel.Format = "Number";
                        RemoveRemoves(labelGroupResult, removedLines);                        
                        
                        return [labelGroupResult];
                    }
                    
                    if (isLicenceNumberLookup && AnyIsLicenceNumber(modifiedPreviousLines, label, out var licenceNumberLines))
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

                        return returnList;
                    }
                    
                    if (isUnitsLookup)
                    {
                        foreach (var previousLine in modifiedPreviousLines)
                        {
                            if (labelGroupResult.MatchedLabel.Possibilities == null)
                            {
                                continue;
                            }
                            
                            foreach (var possibility in labelGroupResult.MatchedLabel.Possibilities!)
                            {
                                if (!previousLine.Text.Contains(possibility,
                                        StringComparison.InvariantCultureIgnoreCase))
                                {
                                    continue;
                                }

                                labelGroupResult.Text = new[] { new DocumentLine(
                                    possibility,
                                    previousLine.LineNumber,
                                    previousLine.PageNumber,
                                    previousLine.Words.ToList()) };
                                labelGroupResult.MatchedLabel.Format = "Units";
                                RemoveRemoves(labelGroupResult, removedLines);
                                labelGroupResult.MatchedLabel.Possibilities = [possibility];
                                
                                return [labelGroupResult];
                            }
                        }
                    }
                    
                    return [];
                },
                1)
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
                    case LabelPosition.Split
                        when expression.Position is LabelPosition.Split:
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
                        return true;
                    default:
                        return expression.Position == LabelPosition.ApplicableToAll;
                }
            })
            .OrderBy(expression =>
            {
                if (expression.Position == LabelPosition.ApplicableToAll)
                {
                    const int MINIMUM_POSITION_FOR_ORDERING_ASCENDING = -1;
                    return MINIMUM_POSITION_FOR_ORDERING_ASCENDING;
                }

                return label.Position switch
                {
                    LabelPosition.TextToFindIsBetweenLabels => -2,
                    LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore =>
                        expression.Position is LabelPosition.LabelIsBeforeTextToFind
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore ? -0.25 : 1,
                    LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter =>
                        expression.Position is LabelPosition.LabelIsAfterTextToFind
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore ? -0.25 : 1,
                    LabelPosition.LabelIsBeforeTextToFind or LabelPosition.ContractIsSuccession
                        => expression.Position is LabelPosition.LabelIsBeforeTextToFind ? 0 : 1,
                    _ => expression.Position == LabelPosition.LabelIsAfterTextToFind ? 0 : 1
                };
            })
            .ThenBy(expression => expression.Order)
            .Select(expression => expression.ResultIfMatched)
            .ToList();
    }

    private static void RemoveRemoves(
        LabelGroupResult labelGroupResult,
        IReadOnlyList<string>? removedLines)
    {
        if (labelGroupResult.MatchedLabel?.Remove == null)
        {
            return;
        }
        
        labelGroupResult.MatchedLabel.Remove =
            labelGroupResult.MatchedLabel.Remove!.Where(removeLine => removedLines?.Contains(removeLine.Text) == true).ToList();

        if (labelGroupResult.MatchedLabel.Remove.Count == 0)
        {
            labelGroupResult.MatchedLabel.Remove = null;
        }
    }

    private static bool IsDateOrPurpose(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        if (text.Contains("aggregate"))
        {
            return true;
        }

        return YearRegex().IsMatch(text);
    }
    
    private static bool AnyIsDateOrPurpose(
        IEnumerable<DocumentLine?> lines,
        out List<DocumentLine> matchedLines)
    {
        var returnValue = false;
        var outList = new List<DocumentLine>();
        
        foreach (var line in lines)
        {
            if (IsDateOrPurpose(line!.Text))
            {
                outList.Add(line);
                returnValue = true;
            }
        }

        matchedLines = outList;
        return returnValue;
    }    

    private async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> text,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        bool useCache)
    {
        var subResults = new List<LabelGroupResult>();
                    
        if (label.SubLabels?.Count > 0)
        {
            if (label.Name == "DocumentPurposesAll")
            {
                
            }
            
            foreach (var subLabel in label.SubLabels)
            {
                if (subLabel.Remove == null && label.Remove != null)
                {
                    subLabel.Remove = label.Remove;
                }
                            
                var subLabelGroupMatch = await FindLabelGroupMatchesInLinesAsync(
                    GetLines(text, subLabel),
                    [subLabel],
                    isOcr,
                    serviceName,
                    labelGroupName,
                    subResults,
                    licenceMapping,
                    previouslyParsedPaths,
                    outputFolder,
                    useCache);
                            
                if (subLabelGroupMatch.Count > 0)
                {
                    subResults.AddRange(subLabelGroupMatch);
                }
            }
        }
        
        var anyDidntStartAtStartOfBlock = subResults.Any(subResult =>
            subResult.MatchedLabel?.Text?.FirstOrDefault() != "[START_OF_BLOCK]");

        if (anyDidntStartAtStartOfBlock)
        {
            subResults = subResults
                .Where(subResult => subResult.MatchedLabel?.Text?.FirstOrDefault() != "[START_OF_BLOCK]")
                .ToList();
        }

        return subResults;
    }
    
    private static string RemoveExcludes(
        LabelToMatch label,
        string betweenText,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        
        if (label.Remove?.Any() != true || string.IsNullOrEmpty(betweenText))
        {
            return betweenText;
        }

        var returnStr = betweenText;
        var removesUsedList = new List<string>();
        
        foreach (var textToMatch in label.Remove)
        {
            if (textToMatch.Text.StartsWith('/') && textToMatch.Text.EndsWith('/'))
            {
                var pattern = textToMatch.Text.Substring(1, textToMatch.Text!.Length - 2);

                if (Regex.IsMatch(returnStr, pattern))
                {
                    returnStr = Regex.Replace(
                        returnStr,
                        pattern,
                        string.Empty);
                    
                    removesUsedList.Add(textToMatch.Text);
                }
                
                continue;
            }

            if (!returnStr.Contains(textToMatch.Text))
            {
                continue;
            }

            if (textToMatch.LineMustStartWith && !returnStr.StartsWith(textToMatch.Text))
            {
                continue;
            }

            if (textToMatch.RemoveWholeLine)
            {
                removesUsedList.Add(returnStr);
                returnStr = string.Empty;
                
                continue;
            }
            returnStr = returnStr.Replace(
                textToMatch.Text,
                string.Empty,
                StringComparison.InvariantCultureIgnoreCase);

            removesUsedList.Add(textToMatch.Text);
        }

        removesUsed = removesUsedList.Count != 0 ? removesUsedList : null;
        return TrimFormatting(returnStr)!;
    }

    private static List<DocumentLine> RemoveExcludes(
        LabelToMatch label,
        IReadOnlyList<DocumentLine>? betweenText,
        out IReadOnlyList<string>? removesUsed)
    {
        removesUsed = null;
        var returnList = betweenText != null ? [..betweenText] : new List<DocumentLine>();
        
        if (label.Remove?.Any() != true || betweenText == null)
        {
            return RemoveMultipleBlankLines(returnList);
        }

        var removesUsedList = new List<string>();
        
        for (var idx = 0; idx < returnList.Count; idx++)
        {
            returnList[idx] = new DocumentLine(
                RemoveExcludes(label, betweenText[idx].Text, out var removesUsedLoop),
                returnList[idx].LineNumber,
                returnList[idx].PageNumber,
                returnList[idx].Words.ToList());

            if (removesUsedLoop != null)
            {
                removesUsedList.AddRange(removesUsedLoop);
            }
        }

        removesUsed = removesUsedList;
        return RemoveMultipleBlankLines(returnList);
    }
    
    private static bool PotentialMatchOnLabelLine(
        IEnumerable<(string? Text, LabelToMatch Label)> textBeforeAndAfterLabel)
    {
        foreach (var (text, _) in textBeforeAndAfterLabel)
        {
            if (!IsNullOrEmptyWhitespaceOrPunctuation(text)
                && text!.Trim() != "-"
                && text.Trim() != "—")
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<(string?, LabelToMatch)> GetLineBeforeAndAfterText(
        DocumentLine line,
        LabelToMatch label)
    {
        var returnItems = new List<(string?, LabelToMatch)>();
        
        if (label.Text == null)
        {
            returnItems.Add((line.Text, label)!);
            return returnItems;
        }
        
        var labelTextPositionIndex = POSITION_NOT_FOUND;
        string? matchedLabelText = null;

        foreach (var labelText in label.Text!)
        {
            try
            {
                var index = line.Text.IndexOf(
                    labelText,
                    StringComparison.InvariantCultureIgnoreCase);

                if (index > POSITION_NOT_FOUND)
                {
                    labelTextPositionIndex = index;
                    matchedLabelText = labelText;
                    
                    break;
                }
            

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        if (labelTextPositionIndex == POSITION_NOT_FOUND)
        {
            return [];
        }

        var textBeforeLabel = TrimFormatting(line.Text[..labelTextPositionIndex]);
        var textAfterLabel = TrimFormatting(line.Text[(labelTextPositionIndex + matchedLabelText!.Length)..]);
        
        if (!string.IsNullOrEmpty(textAfterLabel)
            && label.Position is LabelPosition.LabelIsBeforeTextToFind
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels
                or LabelPosition.ContractIsSuccession
                or LabelPosition.RelatedCategoryPosition
                or LabelPosition.Split)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = label.Position is
                LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels
                    ? LabelPosition.LabelIsBeforeTextToFind
                    : label.Position;
            
            returnItems.Add((textAfterLabel.Trim(), returnLabel));
        }
        
        if (!string.IsNullOrEmpty(textBeforeLabel)
            && label.Position is LabelPosition.LabelIsAfterTextToFind
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels                
                or LabelPosition.ContractIsSuccession
                or LabelPosition.RelatedCategoryPosition
                or LabelPosition.Split)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = label.Position is
                LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels
                    ? LabelPosition.LabelIsAfterTextToFind
                    : label.Position;
            
            returnItems.Add((textBeforeLabel.Trim(), returnLabel));
        }

        return returnItems;
    }
    
    private static bool LineContainsLabel(
        DocumentLine line,
        IReadOnlyList<string>? labelText,
        LabelPosition position,
        int lineCount,
        int howManyLinesTotal,
        out string? matchedText)
    {
        if (labelText == null)
        {
            matchedText = null;
            return true;
        }
        
        foreach (var textItem in labelText)
        {
            if (lineCount == 0
                && textItem.Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase))
            {
                matchedText = textItem;
                return true;
            }

            if (line.Text.Contains("PERIOD OF ABSTRACTION"))
            {
                
            }
            
            if (line.Text.StartsWith(textItem, StringComparison.InvariantCultureIgnoreCase)
                || line.Text.Contains($" {textItem}", StringComparison.InvariantCultureIgnoreCase))
            {
                matchedText = textItem;
                return true;
            }

            if (position == LabelPosition.Split && lineCount == howManyLinesTotal - 1)
            {
                matchedText = null;
                return true;
            }
        }

        matchedText = null;
        return false;
    }

    private static IReadOnlyList<(
            DocumentLine Line,
            IReadOnlyList<DocumentLine> PreviousNLines,
            IReadOnlyList<DocumentLine> NextNLines)>
        GetLines(
            IReadOnlyList<DocumentLine> lines,
            LabelToMatch label)
    {
        return lines.Select((line, index) =>
            (
                new DocumentLine(
                    Standardise(line.Text),
                    line.LineNumber,
                    line.PageNumber,
                    line.Words.ToList()),
                GetPreviousLines(lines, index, label.PreviousLinesToFetch),
                GetNextLines(lines, index, label.NextLinesToFetch)
            ))
            .ToList();
    }
    
    private static IReadOnlyList<DocumentLine> GetPreviousLines(IReadOnlyList<DocumentLine> lines, int index, int n)
    {
        var newIndex = index - 1;
        var returnList = new List<DocumentLine>();
        var count = 0;

        while (newIndex >= 0 && count++ < n)
        {
            returnList.Add(
                new DocumentLine(
                    Standardise(lines[newIndex].Text),
                    lines[newIndex].LineNumber,
                    lines[newIndex].PageNumber,
                    lines[newIndex].Words.ToList()));

            newIndex -= 1;
        }

        return returnList;
    }
    
    private static IReadOnlyList<DocumentLine> GetNextLines(IReadOnlyList<DocumentLine> lines, int index, int n)
    {
        var newIndex = index + 1;
        var returnList = new List<DocumentLine>();
        var count = 0;
        
        while (newIndex < lines.Count && count++ < n)
        {
            returnList.Add(
                new DocumentLine(
                    Standardise(lines[newIndex].Text),
                    lines[newIndex].LineNumber,
                    lines[newIndex].PageNumber,
                    lines[newIndex].Words.ToList()));

            newIndex += 1;
        }

        return returnList;
    }
    
    private static bool LabelIsInDocument(LabelToMatch label, IReadOnlyList<DocumentLine> lines)
    {
        if (label.Text!.Any(text => text.Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase)))
        {
            return true;
        }
        
        return label.Text!.Any(text =>
            Standardise(string.Join(',', lines.Select(line => line.Text))).Contains(text,
                StringComparison.InvariantCultureIgnoreCase));
    }

    [GeneratedRegex(@"19\d\d|20\d\d")]
    private static partial Regex YearRegex();
}