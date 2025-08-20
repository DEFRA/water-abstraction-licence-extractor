using System.Text.Json;
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
            JsonHelper.GetSerializer());

        return metadata!;
    }

    private string GetImageMetadataFilename(PdfDocument pdfDocument)
    {
        var imagesMetadataFolder = $"{pdfDocument.CacheFolder}/{noOcrDataExtractorService.Name}/Images";
        Directory.CreateDirectory(imagesMetadataFolder); // This checks if exists, and creates the whole path too
        return $"{imagesMetadataFolder}/{PositionConstants.CacheMetadataFilename}";
    }
    
    private async Task<(ImageMetadata, bool)> GetImageMetadataAsync(PdfDocument pdfDocument)
    {
        if (pdfDocument.FromCache)
        {
            return (await LoadImageMetadataFromCacheAsync(pdfDocument), false); // TODO load the cached image metadata new ImageMetadata();
        }

        var imagesMetadata = new ImageMetadata();
            
        foreach (var page in pdfDocument.Pages)
        {
            await noOcrDataExtractorService.SavePageScreenshotAsync(pdfDocument, page.Number);
            
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
       
        // Save all text
        if (!pdfDocument.FromCache)
        {
            var outputFolderFull = $"{pdfDocument.OutputFolder}/{noOcrDataExtractorService.Name}";
            var folder = $"{outputFolderFull}/Text";
            Directory.CreateDirectory(folder);
            
            await File.WriteAllTextAsync(
                $"{folder}/pages-all.txt",
                string.Join("\r\n", documentLines
                    .Select(line => $"{line.LineNumber} {line.Text}")
                    .ToArray()));
            
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
            var imageNumber = 1;
            var pageImageNumber = 1;

            var breakOuter = false;
            
            foreach (var imageFilename in page.ImageFiles)
            {
                List<DocumentLine>? bestImageLines = null;
                
                foreach (var ocrService in ocrDataExtractorServices
                    .OrderBy(service => service.HasDirectCost))
                {
                    if (!returnResult.ServicesUsed.Contains(ocrService.Name))
                    {
                        returnResult.ServicesUsed.Add(ocrService.Name);
                    }
                    
                    var imageLines =
                        await ocrService.GetTextLinesFromImageAsync(
                            imageFilename,
                            pageNumber,
                            pageImageNumber++,
                            pdfDocument);
                    
                    var allLinesSoFar = documentLines.ToList();
                    allLinesSoFar.AddRange(imageLines);

                    var providers = returnResult.Pages
                        .Single(p => p.Number == page.Number).Providers;

                    if (providers.All(p => p.Provider != ocrService.Name))
                    {
                        providers.Add(new PdfPageProvider
                        {
                            Provider = ocrService.Name,
                            Text = imageLines.Select(l => l.Text).ToList()
                        });
                    }                    
                    
                    const bool isOcr = true;
                    
                    var ocrResults = await GetLabelGroupMatchesAsync(
                        allLinesSoFar,
                        unmatchedLabelLookups,
                        isOcr,
                        ocrService.Name,
                        configuration.LicenceMapping,
                        previouslyParsedPaths,
                        configuration.OutputFolder,
                        configuration.CacheFolder);

                    var noMatchesFound = ocrResults.Count == 0;
                    bestImageLines = imageLines.ToList();
                    
                    if (noMatchesFound)
                    {
                        continue;
                    }

                    labelGroupMatches.AddRange(ocrResults);
                    
                    foreach (var ocrResult in ocrResults)
                    {
                        var matchedLabel = ocrResult.MatchedLabel!;
                        var ifMultiplePreferLast = matchedLabel.Text!.First().IfMultiplePreferLast;
                        var ifMultiplePreferLongest = matchedLabel.Text!.First().IfMultiplePreferLongest;

                        if (ifMultiplePreferLast || ifMultiplePreferLongest)
                        {
                            var alreadyOutput = labelGroupMatches
                                .Where(r => r.MatchedLabel?.Name == matchedLabel.Name)
                                .ToList();

                            if (alreadyOutput.Count >= 2)
                            {
                                var i = alreadyOutput
                                    .OrderBy(x => ifMultiplePreferLast ? ((x.PageNumber * 100) + x.LineNumber) : x.Text?.Count)
                                    .First();
                        
                                labelGroupMatches.Remove(i);
                            }
                        }
                    }
                    
                    unmatchedLabelLookups = GetUnmatchedLabels(
                        unmatchedLabelLookups,
                        labelGroupMatches,
                        false);
                    
                    var labelsNotMatchedAtAll = GetUnmatchedLabels(
                        unmatchedLabelLookups,
                        labelGroupMatches,
                        true);

                    if (labelsNotMatchedAtAll.Count == 0)
                    {
                        breakOuter = true;
                        break;
                    }
                }

                if (bestImageLines != null)
                {
                    documentLines.AddRange(bestImageLines);
                }
            }

            if (breakOuter)
            {
                break;
            }
            
            unmatchedLabelLookups = GetUnmatchedLabels(
                unmatchedLabelLookups,
                labelGroupMatches,
                false);
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
            JsonSerializer.Serialize(imagesMetadata, JsonHelper.GetSerializer()));
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
        LabelToMatch label,
        int lineCount,
        IReadOnlyList<DocumentLine> previousLines,
        IReadOnlyList<DocumentLine> nextLines)
    {
        var matchedAll = true;
                    
        foreach (var labelText in label.Text!)
        {
            if (LabelMatchingHelper.LineContainsLabel(
                line,
                [labelText],
                label.Position,
                lineCount,
                PositionConstants.UnknownLinesTotal,
                out _))
            {
                continue;
            }

            var continueOuterLoop = false;
                        
            foreach (var previousLine in previousLines)
            {
                if (LabelMatchingHelper.LineContainsLabel(
                    previousLine,
                    [labelText],
                    label.Position,
                    lineCount,
                    PositionConstants.UnknownLinesTotal,
                    out _))
                {
                    continueOuterLoop = true;
                    break;
                }
            }                        
                        
            foreach (var nextLine in nextLines)
            {
                if (LabelMatchingHelper.LineContainsLabel(
                    nextLine,
                    [labelText],
                    label.Position,
                    lineCount,
                    PositionConstants.UnknownLinesTotal,
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
                var partialLine = (DocumentLine?)lineOuter;
                lineCount += 1;

                while (partialLine?.Columns.Any(c => c.Text.Length > 0) == true)
                {
                    var textBeforeAndAfterLabel = new List<(string? Text, LabelToMatch Label)>();
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

                    if (!LabelMatchingHelper.LineContainsLabel(
                            partialLine,
                            label.Text,
                            label.Position,
                            lineCount,
                            totalLineCount,
                            out var matchedStartText))
                    {
                        partialLine = null;
                        continue;
                    }

                    if (LabelMatchingHelper.TextContainsForbiddenLine(partialLine.Text, label))
                    {
                        partialLine = null;
                        continue;
                    }

                    if (label.MatchAllText)
                    {
                        if (ProcessMatchAll(partialLine, label, lineCount, previousLines, nextLines))
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

                    textBeforeAndAfterLabel.AddRange(
                        GetLineBeforeAndAfterText(partialLine, matchedLabel));

                    var lookupExpressions = GetRelevantLookupExpressions(matchedLabel)
                        .ToList();

                    var labelGroupResult = new LabelGroupResult
                    {
                        IsOcr = isOcr,
                        LineNumber = partialLine.LineNumber,
                        PageNumber = partialLine.PageNumber,
                        ServiceName = serviceName
                    };

                    var request = new FunctionInputModel
                    {
                        actsLikeSingleWord = matchedLabel.Format == ActsLikeSingleWord.Constant,
                        textBeforeAndAfterLabel = textBeforeAndAfterLabel,
                        isCompanyType = matchedLabel.Format == CompanyName.Constant,
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
                        lineNumber = partialLine.LineNumber
                    };

                    foreach (var expression in lookupExpressions)
                    {
                        var results = await expression(request);

                        if (results.Count == 0)
                        {
                            continue;
                        }

                        foreach (var result in results)
                        {
                            var newLineNumber = result.Text?.FirstOrDefault()?.LineNumber;

                            if (newLineNumber.HasValue && newLineNumber != result.LineNumber)
                            {
                                result.LineNumber = newLineNumber.Value;
                            }
                        }

                        returnList.AddRange(results.Where(result => result.MatchType != MatchType.NotFound));

                        var ifMultiplePreferLast = matchedLabel.Text?.FirstOrDefault()?.IfMultiplePreferLast ?? false;
                        var ifMultiplePreferLongest =
                            matchedLabel.Text?.FirstOrDefault()?.IfMultiplePreferLongest ?? false;

                        // TOOD there should only be one below - not 2 or more
                        if (ifMultiplePreferLast || ifMultiplePreferLongest)
                        {
                            var alreadyOutput = returnList
                                .Where(r => r.MatchedLabel?.Name == matchedLabel.Name)
                                .ToList();

                            if (alreadyOutput.Count >= 2)
                            {
                                var i = alreadyOutput
                                    .OrderBy(x =>
                                        ifMultiplePreferLast ? ((x.PageNumber * 100) + x.LineNumber) : x.Text?.Count)
                                    .First();

                                returnList.Remove(i);
                            }
                        }

                        if (matchedLabel.Multiple is MultipleType.False)
                        {
                            return returnList;
                        }

                        if (matchedLabel.Position == LabelPosition.TextToFindIsBetweenLabels
                            && results.Count > 0)
                        {
                            var result = results[0];

                            if (result.LineNumber == partialLine?.LineNumber)
                            {
                                var rt = result.Text?.FirstOrDefault()?.Text;

                                if (rt != null)
                                {
                                    var i = partialLine.Text.IndexOf(rt, StringComparison.Ordinal) + rt.Length;

                                    if (partialLine.Text.Length > i)
                                    {
                                        var t = partialLine.Text[i..];

                                        if (t != string.Empty)
                                        {
                                            if (t == "ION")
                                            {

                                            }

                                            partialLine = partialLine.Clone();
                                            partialLine.Columns.Clear();
                                            partialLine.Columns.Add(new DocumentLineColumn(t));

                                            continuePartialLoop = true;
                                        }
                                    }
                                }
                            }
                        }
                    }

                    if (continuePartialLoop)
                    {
                        continue;
                    }

                    partialLine = null;

                    // Don't carry on if we've identified it was a succession document
                    if (matchedLabel.Position == LabelPosition.ContractIsSuccession)
                    {
                        breakLineLoop = true;
                        break;
                    }
                }
            }
            
            if (breakLineLoop)
            {
                break;
            }
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
    
    private IEnumerable<Func<FunctionInputModel, Task<List<LabelGroupResult>>>>
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
                        return expression.Position == LabelPosition.ApplicableToMost
                            && label.Position != LabelPosition.Split
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
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore ? -0.25 : 1,
                    LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter =>
                        expression.Position is LabelPosition.LabelIsAfterTextToFind
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                            or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore ? -0.25 : 1,
                    LabelPosition.LabelIsInMiddleOfTextToFind =>
                        expression.Position is LabelPosition.LabelIsInMiddleOfTextToFind ? -0.25 : 1,
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
        
        var anyDidntStartAtStartOfBlock = subResults.Any(subResult =>
            subResult.MatchedLabel?.Text?.FirstOrDefault()?.Text != "[START_OF_BLOCK]");

        if (anyDidntStartAtStartOfBlock)
        {
            subResults = subResults
                .Where(subResult => subResult.MatchedLabel?.Text?.FirstOrDefault()?.Text != "[START_OF_BLOCK]")
                .ToList();
        }

        return subResults;
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
            line.Text[..labelTextPositionIndex], true);
        
        var textAfterLabel = FormattingHelper.TrimFormatting(
            line.Text[(labelTextPositionIndex + matchedLabelText!.Length)..], true);
        
        if (!string.IsNullOrEmpty(textAfterLabel)
            && label.Position is LabelPosition.LabelIsBeforeTextToFind
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeBefore
                or LabelPosition.LabelIsBeforeAndOrAfterTextToFindPreferLabelToBeAfter
                or LabelPosition.LabelIsInMiddleOfTextToFind
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
                or LabelPosition.LabelIsInMiddleOfTextToFind
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
            .Select(text => text.Text.Replace(PositionConstants.EndOfLineMarker, string.Empty))
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