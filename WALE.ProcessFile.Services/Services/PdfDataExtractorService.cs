using System.Text.Json;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Enums;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Methods;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services.PdfPig;
using MatchType = WALE.ProcessFile.Services.Enums.MatchType;

namespace WALE.ProcessFile.Services.Services;

public class PdfDataExtractorService(
    INoOcrDataExtractorService noOcrDataExtractorService,
    IEnumerable<IOcrDataExtractorService> ocrDataExtractorServices,
    string pdfFolderPath)
    : IPdfDataExtractorService
{
    public bool InUse { get; set; } = false;

    private async Task<ImageMetadata> LoadImageMetadataFromCacheAsync(PdfDocument pdfDocument)
    {
        var metaDataFileText = await File.ReadAllTextAsync(GetImageMetadataFilename(pdfDocument));
        var metadata = JsonSerializer.Deserialize<ImageMetadata>(
            metaDataFileText,
            JsonHelper.GetSerializerOptions());

        return metadata!;
    }

    private string GetImageMetadataFilename(PdfDocument pdfDocument)
    {
        var imagesMetadataFolder = $"{pdfDocument.CacheFolder}/{noOcrDataExtractorService.Name}/Images";
        Directory.CreateDirectory(imagesMetadataFolder); // This checks if exists, and creates the whole path too
        return $"{imagesMetadataFolder}/{PositionConstants.CacheMetadataFilename}";
    }
    
    private async Task<(ImageMetadata imageMetadata, bool imageMetadataChanged)>
        GetImageMetadataAsync(PdfDocument pdfDocument)
    {
        foreach (var page in pdfDocument.Pages)
        {
            var path =
                noOcrDataExtractorService.GetPageScreenshotPath(pdfDocument, page.Number);

            var screenshotPath = path.imgFolder + path.imgOutputFilename;
            
            if (!File.Exists(screenshotPath))
            {
                await noOcrDataExtractorService.SavePageScreenshotAsync(pdfDocument, page.Number);
            }
        }

        if (pdfDocument.FromCache)
        {
            return (await LoadImageMetadataFromCacheAsync(pdfDocument), false);
        }

        var imagesMetadata = new ImageMetadata();
            
        foreach (var page in pdfDocument.Pages)
        {
            var pageImageService = new PdfPigNoOcrPageService(page.PdfPigPage!); // TODO should use the interface (via a factory)
            var metadataPage = new ImageMetadataPage
            {
                Number = page.Number,
                ImageFilename = $"{pdfDocument.CacheFolder}/{page.GetImageFilepath(noOcrDataExtractorService.Name)}"
            };
            
            imagesMetadata.Pages.Add(metadataPage);
            var imageNumber = 1;
            
            foreach (var image in await pageImageService.GetImagesAsync())
            {
                var extension = await image.SaveImageBytesAsync(
                    imageNumber,
                    page.Number,
                    pdfDocument.CacheFolder);

                if (extension == null)
                {
                    continue;
                }
                
                var filepath = image.GetImageFilepath(imageNumber++, page.Number, pdfDocument.CacheFolder, false, extension);
                metadataPage.ImageFiles.Add(filepath);
            }
        }

        return (imagesMetadata, true);
    }
    
    public async Task<MatchesResult> GetMatchesAsync(
        string pdfFilePath,
        LookupConfiguration configuration,
        List<string> previouslyParsedPaths)
    {
        var pdfDocument = await noOcrDataExtractorService.GetPdfDocumentAsync(
            pdfFilePath,
            GetFolderPath(configuration.OutputFolder, pdfFilePath),
            GetFolderPath(configuration.CacheFolder, pdfFilePath));

        var returnResult = new MatchesResult
        {
            Filename = pdfFilePath.Split('/').Last(),
            NumberOfPages = pdfDocument.Pages.Count,
            Pages = pdfDocument.Pages
        };
        
        returnResult.ServicesUsed.Add(noOcrDataExtractorService.Name);
        
        var (imagesMetadata, imageMetadataChanged) =
            await GetImageMetadataAsync(pdfDocument);
        
        var documentLines =
            await noOcrDataExtractorService.GetTextLinesFromPdfAsync(pdfDocument);

        var outputFolderFull = $"{pdfDocument.OutputFolder}/{noOcrDataExtractorService.Name}";
        var folder = $"{outputFolderFull}/Text";
        var pageAllPath = $"{folder}/pages-all.txt";

        if (!File.Exists(pageAllPath))
        {
            Directory.CreateDirectory(folder);
            
            await File.WriteAllTextAsync(
                pageAllPath,
                string.Join("\r\n", documentLines
                    .Select(line => $"{line.LineNumber} {line.Text}")
                    .ToArray()));
        }
        
        var pageAllJsPath = $"{folder}/pages-all.js";

        if (!File.Exists(pageAllJsPath))
        {
            var body = string.Join("\r\n", documentLines
                .Select(line => $"{line.LineNumber} {line.Text}")
                .ToArray());
            
            await File.WriteAllTextAsync(
                pageAllJsPath,
                "var textData = `" + body + "`;");
        }
        
        // Save all text
        if (!pdfDocument.FromCache)
        {
            await SaveImageMetadataAsync(imageMetadataChanged, pdfDocument, imagesMetadata);            
        }

        const bool notOcr = false;
        
        var labelGroupMatches = await GetLabelGroupMatchesAsync(
            documentLines,
            configuration.Labels,
            notOcr,
            noOcrDataExtractorService.Name,
            configuration.LicenceMapping,
            previouslyParsedPaths,
            configuration.OutputFolder,
            configuration.CacheFolder);

        var isTextFile = documentLines.Count >= 100;

        // If it's a text file, we don't need to go off and do image lookups
        if (isTextFile)
        {
            returnResult.Matches = labelGroupMatches;
            return returnResult;            
        }
        
        var unmatchedLabelLookups =
            GetUnmatchedLabels(configuration.Labels, labelGroupMatches, false);
        
        if (unmatchedLabelLookups.Count == 0)
        {
            returnResult.Matches = labelGroupMatches;
            return returnResult;
        }

        returnResult.ScannedFile = true;
        documentLines = [];
        
        var pageNumber = 0;
        
        foreach (var page in imagesMetadata.Pages)
        {
            pageNumber += 1;
            
            var pageImageNumber = 1;
            var breakPageLoop = false;
            
            foreach (var imageFilename in page.ImageFiles)
            {
                // TODO check dimensions and if tiny don't process (Azure AI vision cant cope with it for example)
                
                var breakImageLoop = false;

                var serviceImageLines = new List<DocumentLine>();
                var serviceMatchesDict = new Dictionary<IOcrDataExtractorService, List<LabelGroupResult>>();
                
                foreach (var ocrService in ocrDataExtractorServices
                    .OrderBy(service => service.HasDirectCost))
                {
                    if (!returnResult.ServicesUsed.Contains(ocrService.Name))
                    {
                        returnResult.ServicesUsed.Add(ocrService.Name);
                    }

                    try
                    {
                        serviceImageLines =
                            (await ocrService.GetTextLinesFromImageAsync(
                                imageFilename,
                                pageNumber,
                                pageImageNumber++,
                                pdfDocument)).ToList();
                    }
                    catch (Exception ex)
                    {
                        serviceImageLines = [];
                        
                        Console.WriteLine(ex);
                        // TODO proper logging somewhere
                    }

                    // No lines found, no point processing that with the other services
                    if (serviceImageLines.Count == 0)
                    {
                        break;
                    }

                    var averageLineLength = serviceImageLines.Average(line => line.Text.Length);
                    
                    var allLinesSoFar = documentLines.ToList();
                    allLinesSoFar.AddRange(serviceImageLines);

                    var providers = returnResult.Pages
                        .Single(p => p.Number == page.Number).Providers;

                    if (providers.All(p => p.Provider != ocrService.Name))
                    {
                        providers.Add(new PdfPageProvider
                        {
                            Provider = ocrService.Name,
                            Text = serviceImageLines.Select(l => l.Text).ToList()
                        });
                    }                    
                    
                    const bool isOcr = true;
                    var serviceMatches = await GetLabelGroupMatchesAsync(
                        allLinesSoFar,
                        unmatchedLabelLookups,
                        isOcr,
                        ocrService.Name,
                        configuration.LicenceMapping,
                        previouslyParsedPaths,
                        configuration.OutputFolder,
                        configuration.CacheFolder);
                    
                    serviceMatchesDict.Add(ocrService, serviceMatches);
                    var noMatchesFound = serviceMatches.Count == 0;
                    
                    if (noMatchesFound)
                    {
                        // Short lines indicate it may be a map page,
                        // no point processing that with the other services
                        if (averageLineLength < 30)
                        {
                            break;
                        }
                        
                        continue;
                    }
                    
                    foreach (var ocrResult in serviceMatches)
                    {
                        var matchedLabel = ocrResult.MatchedLabel!;
                        var ifMultiplePreferLast = matchedLabel.Text!.First().IfMultiplePreferLast;
                        var ifMultiplePreferLongest = matchedLabel.Text!.First().IfMultiplePreferLongest;

                        if (ifMultiplePreferLast || ifMultiplePreferLongest)
                        {
                            var alreadyOutput = labelGroupMatches
                                .Where(r => r.MatchedLabel?.Name == matchedLabel.Name)
                                .ToList();

                            if (alreadyOutput.Count >= 1)
                            {
                                var i = alreadyOutput
                                    .OrderBy(x => ifMultiplePreferLast ? ((x.PageNumber * 100) + x.LineNumber) : x.Text?.Count)
                                    .First();
                        
                                labelGroupMatches.Remove(i);
                            }
                        }
                    }
                    
                    var combinedList = labelGroupMatches.ToList();
                    
                    var labelsNotMatchedAtAll = GetUnmatchedLabels(
                        unmatchedLabelLookups,
                        combinedList,
                        true);

                    if (labelsNotMatchedAtAll.Count == 0)
                    {
                        breakImageLoop = true;
                        breakPageLoop = true;

                        break;
                    }

                    // Short lines indicate it may be a map page,
                    // no point processing that with the other services
                    if (averageLineLength < 30)
                    {
                        //break; // TODO commenting out as a tesseract file fails with this - Non-Application Licence Document (08.06.1987).PDF
                    }
                }

                var uniqueServiceMatches = new List<LabelGroupResult>();

                foreach (var kvp in serviceMatchesDict.OrderBy(x => x.Key.HasDirectCost)) // TODO should be OrderByDescending
                {
                    var serviceMatches = kvp.Value;

                    foreach (var match in serviceMatches)
                    {
                        var alreadyFound = uniqueServiceMatches
                            .FirstOrDefault(x => x.LabelGroupName == match.LabelGroupName);

                        if (alreadyFound != null)
                        {
                            uniqueServiceMatches.Remove(alreadyFound);
                        }

                        uniqueServiceMatches.Add(match);
                    }
                }
                
                documentLines.AddRange(serviceImageLines);
                labelGroupMatches.AddRange(uniqueServiceMatches);
                
                unmatchedLabelLookups = GetUnmatchedLabels(
                    unmatchedLabelLookups,
                    labelGroupMatches,
                    false);
                    
                var labelsNotMatchedAtAll2 = GetUnmatchedLabels(
                    unmatchedLabelLookups,
                    labelGroupMatches,
                    true);

                if (labelsNotMatchedAtAll2.Count == 0)
                {
                    breakPageLoop = true;
                    break;
                }
                
                if (breakImageLoop)
                {
                    break;
                }
            }

            unmatchedLabelLookups = GetUnmatchedLabels(
                unmatchedLabelLookups,
                labelGroupMatches,
                false);
            
            var labelsNotMatchedAtAll3 = GetUnmatchedLabels(
                unmatchedLabelLookups,
                labelGroupMatches,
                true);

            if (labelsNotMatchedAtAll3.Count == 0)
            {
                break;
            }
            
            if (breakPageLoop)
            {
                break;
            }
        }

        await SaveImageMetadataAsync(imageMetadataChanged, pdfDocument, imagesMetadata);
        noOcrDataExtractorService.Release(pdfDocument);

        returnResult.Matches = labelGroupMatches;
        return returnResult;      
    }
    
    private static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetUnmatchedLabels(
        List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
        List<LabelGroupResult> labelGroupMatches,
        bool onlyNotFoundAtAll)
    {
        return labels
            .Where(labelLookup =>
            {
                var doesntMatchAnyFound = labelGroupMatches.All(r =>
                    r.LabelGroupName != labelLookup.LabelGroupName);
                
                var labelFull = labelGroupMatches.FirstOrDefault(lgm =>
                    lgm.MatchedLabel != null
                    && labelLookup.Labels.Any(l => l.Name == lgm.MatchedLabel.Name))?.MatchedLabel;

                var ifMultiplePreferLast = labelFull?.Text?.FirstOrDefault()?.IfMultiplePreferLast ?? false;
                var ifMultiplePreferLongest = labelFull?.Text?.FirstOrDefault()?.IfMultiplePreferLongest ?? false;                
                var canGoOverPageBoundary = labelFull?.CanGoOverPageBoundary ?? false;
                
                if (ifMultiplePreferLast || ifMultiplePreferLongest)
                {
                    
                }
                
                return doesntMatchAnyFound
                    || (!onlyNotFoundAtAll && (ifMultiplePreferLast || ifMultiplePreferLongest || canGoOverPageBoundary));
            })
            .ToList();
    }
    
    private async Task SaveImageMetadataAsync(bool anyChanges, PdfDocument pdfDocument, ImageMetadata imagesMetadata)
    {
        if (!anyChanges)
        {
            return;
        }
        
        await File.WriteAllTextAsync(
            GetImageMetadataFilename(pdfDocument),
            JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializerOptions()));
    }
    
    private async Task<List<LabelGroupResult>> GetLabelGroupMatchesAsync(
        IReadOnlyList<DocumentLine> documentLines,
        IEnumerable<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        bool isOcr,
        string serviceName,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        string cacheFolder)
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
                    cacheFolder);

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
    
    private static string GetFolderPath(string outputFolder, string pdfFilePath)
    {
        try
        {
            var fileOutputFolder = Path.Combine(outputFolder, FileHelper.GetFilenameWithoutExtensions(pdfFilePath));
            if (fileOutputFolder.StartsWith('/'))
            {
                fileOutputFolder = fileOutputFolder[1..];
            }

            return fileOutputFolder;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }

    private static bool AlreadyMatchedLabelGroup(
        IEnumerable<LabelGroupResult> returnList,
        string type)
    {
        return returnList.Any(returnItem => returnItem.LabelGroupName == type);
    }

    private async Task<IReadOnlyList<LabelGroupResult>> ProcessLinkedLicenceAsync(
        DocumentLine line,
        IReadOnlyList<LabelGroupResult> siblingMatches,
        LabelToMatch label,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        string cacheFolder)
    {
        var returnList = new List<LabelGroupResult>();
        
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
                    // TODO this should log a warning
                    continue;
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
                new LookupConfiguration(LabelConfiguration.GetLabels(), licenceMapping, outputFolder, cacheFolder),
                previouslyParsedPaths);

            var labelResult = new LabelGroupResult
            {
                MatchedLabel = label,
                SubResults = relatedFileMatches.Matches!,
                PageNumber = line.PageNumber
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

    private bool ProcessMatchAll(
        DocumentLine line,
        DocumentLine lineForPosition,
        LabelToMatch label,
        int lineCount,
        IReadOnlyList<DocumentLine> previousLines,
        IReadOnlyList<DocumentLine> nextLines)
    {
        var matchedAll = true;
                    
        foreach (var labelText in label.Text!)
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
        string cacheFolder)
    {
        var returnList = new List<LabelGroupResult>();

        var lineCount = -1;
        var totalLineCount = lines.Count;

        foreach (var (lineOuter, previousLines, nextLines) in lines)
        {
            var breakLineLoop = false;
            
            foreach (var label in labels.Where(whereLabel => !whereLabel.Completed))
            {
                if (label.Name == "Issuer")
                {
                    
                }
                
                var partialLine = (DocumentLine?)lineOuter;
                DocumentLine? previousPartialLine = null;
                
                lineCount += 1;

                while (partialLine?.Columns.Any(c => c.Text.Length > 0) == true)
                {
                    if (previousPartialLine?.Text == partialLine.Text)
                    {
                        throw new Exception("Infinite loop detected - coding error");
                    }

                    previousPartialLine = partialLine;
                    
                    var textBeforeAtAndAfterLabel = new List<TextAndLabel>();
                    var continuePartialLoop = false;
                    var matchedLabel = label;

                    if (label.Format == "LinkedLicence")
                    {
                        var linkedLicences = await ProcessLinkedLicenceAsync(
                            partialLine,
                            siblingMatches,
                            label,
                            licenceMapping,
                            previouslyParsedPaths,
                            outputFolder,
                            cacheFolder);

                        returnList.AddRange(linkedLicences);

                        partialLine = null;
                        continue;
                    }

                    if (FormattingHelper.IsLineEmpty(partialLine)
                        && label.Text?.Any(text =>
                            text.Text.Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase)) != true
                        && !(label.Position == LabelPosition.Split && lineCount == totalLineCount - 1))
                    {
                        partialLine = null;
                        continue;
                    }

                    TextToMatch? matchedStartText = null;
                    var labelCharPosition = 0;
                    
                    if (label.Text?.Any() == true)
                    {
                        var nextLine = nextLines.FirstOrDefault();
                        
                        if (!LabelMatchingHelper.LineContainsLabel(
                            partialLine,
                            nextLine,
                            lineOuter,
                            label.Text,
                            label.Position,
                            lineCount,
                            totalLineCount,
                            out matchedStartText,
                            out labelCharPosition))
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
                        if (ProcessMatchAll(partialLine, lineOuter, label, lineCount, previousLines, nextLines))
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
                    
                    textBeforeAtAndAfterLabel.AddRange(
                        GetLineBeforeAtAndAfterText(partialLine, matchedLabel));

                    var lookupExpressions = GetRelevantLookupExpressions(matchedLabel)
                        .ToList();
                    
                    var labelGroupResult = new LabelGroupResult
                    {
                        IsOcr = isOcr,
                        LineNumber = partialLine.LineNumber,
                        CharPosition = labelCharPosition,
                        PageNumber = partialLine.PageNumber,
                        ServiceName = serviceName
                    };

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
                        licenceMapping = licenceMapping,
                        pdfDataExtractorService = this,
                        previouslyParsedPaths = previouslyParsedPaths,
                        previousLines = previousLines,
                        nextLines = nextLines,
                        serviceName = serviceName,
                        siblingMatches = siblingMatches,
                        outputFolder = outputFolder,
                        cacheFolder = cacheFolder,
                        isSingleWord = matchedLabel.Format == SingleWord.Constant,
                        isUnitsLookup = matchedLabel.Format == Units.Constant,
                        line = partialLine,
                        lineForPosition = lineOuter,
                        lineNumber = partialLine.LineNumber
                    };
                    
                    var singleValueWanted = matchedLabel.MultipleBehaviour is
                        MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValue
                        or MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel;
                    
                    foreach (var expression in lookupExpressions)
                    {
                        var result = await ProcessExpressionResultAsync(
                            expression.Value,
                            request,
                            partialLine!,
                            singleValueWanted);

                        if (request.label.FindMultipleOnSingleLine
                            && request.textBeforeAtAndAfterLabel.Count >= 1
                            && request.label.Position is not LabelPosition.Split
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

                    // Don't carry on if we've identified it was a succession document
                    /*if (matchedLabel.Position == LabelPosition.ContractIsSuccession)
                    {
                        breakLineLoop = true;
                        break;
                    }*/
                }
            }
            
            if (breakLineLoop)
            {
                break;
            }
        }

        var atLeastOneResultFound = returnList.Count > 1;
        
        var allAreSingleLabelMultipleLines = returnList.All(match =>
            match.MatchedLabel?.MultipleBehaviour is
                MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValueButMultipleLines);

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
                    MatchType = returnItem.MatchType,
                    PageNumber = returnItem.PageNumber,
                    ServiceName = returnItem.ServiceName,
                    Text = textList
                }
            ];
        }
        
        return returnList;
    }

    private static List<LabelGroupResult> FilterDownResults(List<LabelGroupResult> returnList, LabelToMatch? label)
    {
        // De-dupe exact matches
        returnList = returnList
            .GroupBy(x => x.MatchedLabel!.FindMultipleOnSingleLine ?
                $"{x.PageNumber}_{x.LineNumber}_{x.CharPosition}_{x.MatchedLabel?.Name}_{x.Text?.FirstOrDefault()?.Text}"
                : $"{x.PageNumber}_{x.LineNumber}_{x.MatchedLabel?.Name}_{x.Text?.FirstOrDefault()?.Text}")
            .Select(x => x.OrderByDescending(y => y.MatchedLabel?.Text?.FirstOrDefault()?.Text == "[START_OF_BLOCK]" ? 0 : 1).First())
            .ToList();
        
        var ifMultiplePreferLast = label!.Text?.FirstOrDefault()?.IfMultiplePreferLast ?? false;
        var ifMultiplePreferLongest =
            label.Text?.FirstOrDefault()?.IfMultiplePreferLongest ?? false;

        // TOOD there should only be one below - not 2 or more
        if (!ifMultiplePreferLast && !ifMultiplePreferLongest) return returnList;
        
        var alreadyOutput = returnList
            .Where(r => r.MatchedLabel?.Name == label.Name)
            .ToList();

        if (alreadyOutput.Count >= 2)
        {
            var i = alreadyOutput
                .OrderBy(x =>
                    ifMultiplePreferLast ? ((x.PageNumber * 100) + x.LineNumber) : x.Text?.Count)
                .First();

            returnList.Remove(i);
        }

        return returnList;
    }
    
    private async Task<ExpressionResult> ProcessExpressionResultAsync(
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

            if (newLineNumber.HasValue && newLineNumber != result.LineNumber)
            {
                result.LineNumber = newLineNumber.Value;
            }
        }

        if (singleValueWanted && results.Count >= 1)
        {
            if (request.label!.MultipleBehaviour is
                MultipleBehaviour.FindSingleInstanceOfLabelWithASingleValue)
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
                        
        returnList.AddRange(results.Where(result => result.MatchType != MatchType.NotFound));

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

            if (result.LineNumber == partialLine.LineNumber)
            {
                var resultText = result.Text?.FirstOrDefault()?.Text;

                if (resultText != null)
                {
                    var startIndexOfMatch =
                        partialLine.Text.IndexOf(resultText,
                            StringComparison.InvariantCultureIgnoreCase);

                    var endIndexOfMatch = startIndexOfMatch + resultText.Length;

                    if (startIndexOfMatch > -1 && partialLine.Text.Length > endIndexOfMatch)
                    {
                        var newPartialLineText = partialLine.Text[endIndexOfMatch..];

                        if (newPartialLineText != string.Empty)
                        {
                            partialLine = partialLine.Clone();
                            partialLine.Columns.Clear();
                            partialLine.Columns.Add(new DocumentLineColumn(newPartialLineText));

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
    
    private Dictionary<LabelPosition, Func<FunctionInputModel, Task<List<LabelGroupResult>>>>
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
            (LabelPosition.Split, Split.FunctionAsync, 0),
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
                    case LabelPosition.LabelIsInMiddleOfTextToFind
                        when expression.Position is LabelPosition.LabelIsInMiddleOfTextToFind:
                        return true;
                    default:
                        return expression.Position is LabelPosition.ApplicableToMost
                           && label.Position != LabelPosition.Split
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

    public async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> text,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        Dictionary<string, string> licenceMapping,
        List<string> previouslyParsedPaths,
        string outputFolder,
        string cacheFolder)
    {
        var subResults = new List<LabelGroupResult>();
                    
        if (label.SubLabels?.Count > 0)
        {
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
                    cacheFolder);

                if (subLabelGroupMatch.Count > 0)
                {
                    subResults.AddRange(subLabelGroupMatch);
                }
            }
        }

        var groups = subResults
            .GroupBy(x => x.MatchedLabel!.Name)
            .Where(x => x.Count() > 1)
            .ToList();

        if (groups.Any())
        {
            foreach (var group in groups)
            {
                var anyDidntStartAtStartOfBlock = group.Any(subResult =>
                    subResult.MatchedLabel?.Text?.FirstOrDefault()?.Text != "[START_OF_BLOCK]");
                
                var anyDidStartAtStartOfBlock = group.Any(subResult =>
                    subResult.MatchedLabel?.Text?.FirstOrDefault()?.Text == "[START_OF_BLOCK]");

                if (anyDidntStartAtStartOfBlock && anyDidStartAtStartOfBlock)
                {
                    subResults = subResults
                        .Where(subResult => subResult.MatchedLabel?.Text?.FirstOrDefault()?.Text != "[START_OF_BLOCK]")
                        .ToList();
                }                
            }
        }

        return subResults;
    }

    private static IEnumerable<TextAndLabel> GetLineBeforeAtAndAfterText(
        DocumentLine line,
        LabelToMatch label)
    {
        var returnItems = new List<TextAndLabel>();
        
        var isStartOfBlock = label.Text?.FirstOrDefault()?.Text
            .Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase) == true;

        if (label.Text == null || isStartOfBlock)
        {
            returnItems.Add(new TextAndLabel
            {
                Text = line.Text,
                Label = label
            });
            
            return returnItems;
        }
        
        if (label.Text?.FirstOrDefault()?.IsRegularExpression == true && label.Position == LabelPosition.ActuallyLabel)
        {
            var matches = Regex.Matches(line.Text, label.Text!.FirstOrDefault()!.Text);
                
            var position = line.Text.IndexOf(
                matches[0].Value,
                StringComparison.InvariantCultureIgnoreCase);

            var beforeText = line.Text.Substring(0, position);
            var beforeLabel = label.Clone();
            beforeLabel.Position = LabelPosition.LabelIsAfterTextToFind;
            
            returnItems.Add(new TextAndLabel
            {
                Text = beforeText,
                Label = beforeLabel
            });
            
            returnItems.Add(new TextAndLabel
            {
                Text = matches.FirstOrDefault()?.Value,
                Label = label
            });

            if (line.Text.Length > position + matches.FirstOrDefault()?.Value.Length + 1)
            {
                var afterLabel = label.Clone();
                var afterText = line.Text.Substring(position + matches.FirstOrDefault()!.Value.Length);
                beforeLabel.Position = LabelPosition.LabelIsBeforeTextToFind;

                returnItems.Add(new TextAndLabel
                {
                    Text = afterText,
                    Label = afterLabel
                });
            }

            return returnItems; 
        }
        
        var labelTextPositionIndex = PositionConstants.PositionNotFound;
        string? matchedLabelText = null;

        foreach (var labelText in label.Text!)
        {
            var index = line.Text.IndexOf(
                labelText.Text,
                StringComparison.InvariantCultureIgnoreCase);

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
        
        var textBeforeLabel = FormattingHelper.TrimFormatting(
            line.Text[..labelTextPositionIndex], true, true);

        var textAtLabel = matchedLabelText;
        
        var textAfterLabel = FormattingHelper.TrimFormatting(
            line.Text[(labelTextPositionIndex + matchedLabelText!.Length)..], false, false);
        
        if (!string.IsNullOrEmpty(textBeforeLabel)
            && label.Position is LabelPosition.LabelIsAfterTextToFind
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.LabelIsInMiddleOfTextToFind
                or LabelPosition.TextToFindIsBetweenLabels
                or LabelPosition.ContractIsSuccession
                or LabelPosition.RelatedCategoryPosition
                or LabelPosition.ApplicableToMost                
                or LabelPosition.Split)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = label.Position is
                LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels
                ? LabelPosition.LabelIsAfterTextToFind
                : label.Position;
            
            returnItems.Add(new TextAndLabel
            {
                Text = textBeforeLabel.Trim(),
                Label = returnLabel
            });
        }

        if (!string.IsNullOrEmpty(textAtLabel) && label.IncludeStartLabelText)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = LabelPosition.ActuallyLabel;
            
            returnItems.Add(new TextAndLabel
            {
                Text = textAtLabel.Trim(),
                Label = returnLabel
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
                or LabelPosition.Split)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = label.Position is
                LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.TextToFindIsBetweenLabels
                ? LabelPosition.LabelIsBeforeTextToFind
                : label.Position;
            
            returnItems.Add(new TextAndLabel
            {
                Text = textAfterLabel.Trim(),
                Label = returnLabel
            });
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
            {
                FormattingHelper.Standardise(line.Columns);
                
                return (
                    line,
                    GetPreviousLines(lines, index, label.PreviousLinesToFetch),
                    GetNextLines(lines, index, label.NextLinesToFetch)
                );
            })
            .ToList();
    }
    
    private static IReadOnlyList<DocumentLine> GetPreviousLines(IReadOnlyList<DocumentLine> lines, int index, int n)
    {
        var newIndex = index - 1;
        var returnList = new List<DocumentLine>();
        var count = 0;

        while (newIndex >= 0 && count++ < n)
        {
            var line = lines[newIndex];
            FormattingHelper.Standardise(line.Columns);

            returnList.Add(line);
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
            var line = lines[newIndex];
            FormattingHelper.Standardise(line.Columns);
            
            returnList.Add(line);
            newIndex += 1;
        }

        return returnList;
    }
    
    private static bool LabelIsInDocument(LabelToMatch label, IReadOnlyList<DocumentLine> lines)
    {
        var labelText = label.Text!
            .Select(labelTextMatch =>
            {
                var text = labelTextMatch.Text;

                if (text.Contains(PositionConstants.EndOfLineMarker))
                {
                    text = text
                        .Replace(PositionConstants.EndOfLineMarker, string.Empty);
                }
                
                if (text.Contains(PositionConstants.EndOfColumnMarker))
                {
                    text = text
                        .Replace(PositionConstants.EndOfColumnMarker, string.Empty);
                }
                
                return text;
                
                
            })
            .ToList();
        
        if (labelText.Any(text =>
            text.Equals(PositionConstants.StartOfBlockMarker, StringComparison.InvariantCultureIgnoreCase)))
        {
            return true;
        }

        foreach (var line in lines)
        {
            FormattingHelper.Standardise(line.Columns);
        }
        
        return labelText.Any(text => string.Join(',', lines.Select(line => line.Text)).Contains(text,
            StringComparison.InvariantCultureIgnoreCase));
    }
    
    public void Dispose()
    {
        foreach (var ocrDataExtractorService in ocrDataExtractorServices)
        {
            ocrDataExtractorService.Dispose();
        }
        
        GC.SuppressFinalize(this);
    }
}