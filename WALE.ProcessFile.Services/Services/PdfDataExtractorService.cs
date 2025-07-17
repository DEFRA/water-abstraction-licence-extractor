using System.Text.Json;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Methods;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Services;

public class PdfDataExtractorService(
    INoOcrDataExtractorService noOcrDataExtractorService,
    IEnumerable<IOcrDataExtractorService> ocrDataExtractorServices,
    string pdfFolderPath)
    : IPdfDataExtractorService
{
    public bool InUse { get; set; } = false;
    
    public async Task<MatchesResult> GetMatchesAsync(
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

        var returnResult = new MatchesResult
        {
            Filename = pdfFilePath,
            NumberOfPages = pdfDocument.Pages.Count,
        };
        
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
            returnResult.Matches = labelGroupMatches;
            return returnResult;
        }

        returnResult.ScannedFile = true;
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
                    foreach (var ocrService in ocrDataExtractorServices
                        .OrderBy(service => service.HasDirectCost))
                    {
                        if (!returnResult.ServicesUsed.Contains(ocrService.Name))
                        {
                            returnResult.ServicesUsed.Add(ocrService.Name);
                        }

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
                        if (!returnResult.ServicesUsed.Contains(ocrService.Name))
                        {
                            returnResult.ServicesUsed.Add(ocrService.Name);
                        }
                        
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

        returnResult.Matches = labelGroupMatches;
        return returnResult;      
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
                            SubResults = relatedFileMatches.Matches,
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
                        if (LineContainsLabel(line, [labelText], label.Position, lineCount, PositionConstants.UNKNOWN_LINES_TOTAL, out _))
                        {
                            continue;
                        }

                        var continueOuterLoop = false;
                        
                        foreach (var previousLine in previousLines)
                        {
                            if (LineContainsLabel(previousLine, [labelText], label.Position, lineCount, PositionConstants.UNKNOWN_LINES_TOTAL, out _))
                            {
                                continueOuterLoop = true;
                                break;
                            }
                        }                        
                        
                        foreach (var nextLine in nextLines)
                        {
                            if (LineContainsLabel(nextLine, [labelText], label.Position, lineCount, PositionConstants.UNKNOWN_LINES_TOTAL, out _))
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

            var lookupExpressions = GetRelevantLookupExpressions(matchedLabel);
            
            var labelGroupResult = new LabelGroupResult
            {
                IsOcr = isOcr,
                LineNumber = line.LineNumber,
                PageNumber = line.PageNumber,
                ServiceName = serviceName
            };
            
            foreach (var expression in lookupExpressions)
            {
                var request = new FunctionInputModel
                {
                    actsLikeSingleWord = matchedLabel.Format == "ActsLikeSingleWord",
                    textBeforeAndAfterLabel = textBeforeAndAfterLabel,
                    isCompanyType = matchedLabel.Format == "CompanyName",
                    isDateOrPurposeLookup = matchedLabel.Format == "DateOrPurpose",
                    isLicenceNumberLookup = matchedLabel.Format == "LicenceNumber",
                    isNumberLookup = matchedLabel.Format == "Number",
                    isOcr = isOcr,
                    label = matchedLabel,
                    labelGroupName = labelGroupName,
                    labelGroupResult = labelGroupResult,
                    licenceMapping = licenceMapping,
                    pdfDataExtractorService = this,
                    previouslyParsedPaths = previouslyParsedPaths,
                    previousLines = previousLines,
                    nextLines = nextLines,
                    serviceName = serviceName,
                    siblingMatches = siblingMatches,
                    useCache = useCache,
                    outputFolder = outputFolder,
                    isSingleWord = matchedLabel.Format == "SingleWord",
                    isUnitsLookup = matchedLabel.Format == "Units",
                    line = line,
                    lineNumber = line.LineNumber
                };
                
                var results = await expression(request);

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
    
    private IEnumerable<Func<FunctionInputModel, Task<List<LabelGroupResult>>>> GetRelevantLookupExpressions(LabelToMatch label)
    {
        var expressions = new List<(
            LabelPosition Position,
            Func<
                FunctionInputModel,
                Task<List<LabelGroupResult>>> ResultIfMatched,
            int Order)>
        {
            (LabelPosition.ApplicableToAll, ApplicableToAll.FunctionAsync, 0),
            (LabelPosition.Split, Split.FunctionAsync, 0),
            (LabelPosition.RelatedCategoryPosition, RelatedCategoryPosition.FunctionAsync, 0),
            (LabelPosition.TextToFindIsBetweenLabels, TextToFindIsBetweenLabels.FunctionAsync, 0),
            (LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore, LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore.FunctionAsync, -1),
            (LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter, LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter.FunctionAsync, -1),
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

    public async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
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

    public void Dispose()
    {
        foreach (var ocrDataExtractorService in ocrDataExtractorServices)
        {
            ocrDataExtractorService.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }

    private static IEnumerable<(string?, LabelToMatch)> GetLineBeforeAndAfterText(
        DocumentLine line,
        LabelToMatch label)
    {
        var returnItems = new List<(string?, LabelToMatch)>();
        
        if (label.Text == null)
        {
            returnItems.Add((line.Text, label));
            return returnItems;
        }
        
        var labelTextPositionIndex = PositionConstants.POSITION_NOT_FOUND;
        string? matchedLabelText = null;

        foreach (var labelText in label.Text!)
        {
            try
            {
                var index = line.Text.IndexOf(
                    labelText,
                    StringComparison.InvariantCultureIgnoreCase);

                if (index > PositionConstants.POSITION_NOT_FOUND)
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

        if (labelTextPositionIndex == PositionConstants.POSITION_NOT_FOUND)
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
}