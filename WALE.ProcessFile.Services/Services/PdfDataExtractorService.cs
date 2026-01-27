using System.Text.Json;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.Configuration;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Methods;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services.PdfPig;
using LinkedLicence = WALE.ProcessFile.Services.Formats.LinkedLicence;
using MatchType = WALE.ProcessFile.Core.Enums.MatchType;

namespace WALE.ProcessFile.Services.Services;

public class PdfDataExtractorService(
    INoOcrDataExtractorService noOcrDataExtractorService,
    IEnumerable<IOcrDataExtractorService> ocrDataExtractorServices,
    ICacheService cacheService,
    IOutputService outputService,
    string pdfFolderPath,
    int id = -1) : IPdfDataExtractorService
{
    public int Id { get; set; } = id;
    public bool InUse { get; set; } = false;
    public static string Name => "PdfPig";
    
    public async Task<MatchesResult> GetMatchesAsync(
        string pdfFilePath,
        LookupConfiguration configuration,
        List<string> previouslyParsedPaths,
        int processRunId)
    {
        var pdfDocument = await noOcrDataExtractorService.GetPdfDocumentAsync(
            pdfFilePath,
            outputService,
            cacheService,
            processRunId);

        var returnResult = new MatchesResult
        {
            Filename = FileHelper.GetFilenameWithExtension(pdfFilePath),
            NumberOfPages = pdfDocument.Pages.Count,
            Pages = pdfDocument.Pages,
            RegionCode = configuration.RegionCode
        };
        
        returnResult.ServicesUsed.Add(noOcrDataExtractorService.Name);
        
        var (imagesMetadata, imageMetadataChanged) =
            await GetImageMetadataAsync(pdfDocument, processRunId);
        
        var documentLines =
            await noOcrDataExtractorService.GetTextLinesFromPdfAsync(
                pdfDocument,
                cacheService,
                processRunId);

        await outputService.SaveAllPagesTextIfDoesntExistAsync(documentLines, pdfFilePath, Name, processRunId);
        
        // Save all text
        if (!pdfDocument.FromCache)
        {
            await SaveImageMetadataIfChangedAsync(imageMetadataChanged, pdfDocument, imagesMetadata, processRunId);            
        }

        const bool notOcr = false;
        const int minAverageLineLength = 15;
        
        var labelGroupMatches = await GetLabelGroupMatchesAsync(
            documentLines,
            configuration.Labels,
            notOcr,
            noOcrDataExtractorService.Name,
            configuration.LicenceNumberMapping,
            previouslyParsedPaths,
            configuration.RegionCode,
            processRunId);

        // De-dupe
        var newLabelGroupMatches = new List<LabelGroupResult>();

        foreach (var labelGroupMatch in labelGroupMatches)
        {
            var exists = newLabelGroupMatches.Any(lgm =>
                lgm.LabelGroupName == labelGroupMatch.LabelGroupName
                && lgm.Text?.FirstOrDefault()?.Text == labelGroupMatch.Text?.FirstOrDefault()?.Text);

            if (exists)
            {
                continue;
            }
            
            newLabelGroupMatches.Add(labelGroupMatch);
        }

        labelGroupMatches = newLabelGroupMatches;
        
        var allImagesInDocument = await cacheService.GetImagesAsync(
            new OcrServiceImageDataCacheRequest
            {
                Filepath =  pdfFilePath,
                NoOcrServiceName = Name
            });
        
        var isTextFile = documentLines.Count >= 100;

        // Some PDFs have a text component but are mainly scans (not sure how this has come about)
        // So we need to work out if it's predominately a text file (and there are no big images), we don't need to go off and do image lookups
        if (isTextFile)
        {
            // There are no images
            if (allImagesInDocument.Count == 0)
            {
                returnResult.Matches = labelGroupMatches;
                return returnResult;
            }

            var anyImageLargeEnoughToBePageScan = true;

            for (var pageNumberIndex = 0; pageNumberIndex < imagesMetadata.Pages.Count; pageNumberIndex++)
            {
                var page = imagesMetadata.Pages[pageNumberIndex];
                var pageNumber = pageNumberIndex + 1;
                
                for (var imageNumberIndex = 0; imageNumberIndex < page.Images.Count; imageNumberIndex++)
                {
                    var imageNumber = imageNumberIndex + 1;
                    var image = allImagesInDocument
                        .First(i => i.pageNumber == pageNumber && i.imageNumber == imageNumber);

                    if (!IsPageScan(image.width, image.height))
                    {
                        continue;
                    }

                    anyImageLargeEnoughToBePageScan = true;
                    break;
                }
                
                if (anyImageLargeEnoughToBePageScan)
                {
                    break;
                }
            }
            
            if (!anyImageLargeEnoughToBePageScan)
            {
                returnResult.Matches = labelGroupMatches;
                return returnResult;
            }
        }

        var unmatchedOrMoreWantedLabelLookups =
            GetUnmatchedOrMoreWantedLabels(configuration.Labels, labelGroupMatches, false);
        
        if (unmatchedOrMoreWantedLabelLookups.Count == 0)
        {
            returnResult.Matches = labelGroupMatches;
            return returnResult;
        }

        returnResult.ScannedFile = true;
        documentLines = [];
        
        for (var pageNumberIndex = 0; pageNumberIndex < imagesMetadata.Pages.Count; pageNumberIndex++)
        {
            var page = imagesMetadata.Pages[pageNumberIndex];
            var pageNumber = pageNumberIndex + 1;
            
            var breakPageLoop = false;
            var pageImages = page.Images.ToList();
            
            if (pageImages.Count > 10)
            {
                pageImages = [page.ImageReference!];
            }

            for (var imageNumberIndex = 0; imageNumberIndex < pageImages.Count; imageNumberIndex++)
            {
                var imageReference = pageImages[imageNumberIndex];
                var imageNumber = imageNumberIndex + 1;
                
                var image = allImagesInDocument
                    .First(i => i.pageNumber == pageNumber && i.imageNumber == imageNumber);
                
                if (!IsPageScan(image.width, image.height))
                {
                    continue;
                }
                
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
                                imageReference,
                                pdfFilePath,
                                pageNumber,
                                imageNumber,
                                pdfDocument,
                                processRunId,
                                Name)).ToList();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex);
                        
                        // TODO proper logging somewhere
                        throw;
                    }
                    
                    // No lines found, no point processing that with the other services
                    if (serviceImageLines.Count == 0)
                    {
                        break;
                    }
                    
                    var containsTheWordMap = serviceImageLines
                        .Any(l => l.Text.Contains("Map accompanying ", StringComparison.InvariantCultureIgnoreCase)
                            || l.Text.Contains("Location Map ", StringComparison.InvariantCultureIgnoreCase));

                    if (containsTheWordMap)
                    {
                        serviceImageLines = [];
                        break;
                    }
                    
                    var averageLineLength = serviceImageLines.Average(line => line.Text.Length);
                    
                    // Short lines indicate it may be a map page,
                    // no point processing that with the other services
                    if (averageLineLength < minAverageLineLength)
                    {
                        serviceImageLines = [];                        
                        break;
                    }
                    
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
                        unmatchedOrMoreWantedLabelLookups,
                        isOcr,
                        ocrService.Name,
                        configuration.LicenceNumberMapping,
                        previouslyParsedPaths,
                        configuration.RegionCode,
                        processRunId);
                    
                    serviceMatchesDict.Add(ocrService, serviceMatches);
                    var noMatchesFound = serviceMatches.Count == 0;
                    
                    if (noMatchesFound)
                    {
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
                    combinedList.AddRange(serviceMatches);
                    
                    var labelsNotMatchedAtAll = GetUnmatchedOrMoreWantedLabels(
                        unmatchedOrMoreWantedLabelLookups,
                        combinedList,
                        true);

                    if (labelsNotMatchedAtAll.Count == 0)
                    {
                        breakImageLoop = true;
                        breakPageLoop = true;

                        break;
                    }
                }
                
                documentLines.AddRange(serviceImageLines);

                var uniqueServiceMatches = GetUniqueServiceMatches(serviceMatchesDict);
                var uniqueServiceMatchesNotInLabelGroupMatches = new List<LabelGroupResult>();

                foreach (var uniqueServiceMatch in uniqueServiceMatches)
                {
                    var exists = labelGroupMatches.Any(lgm =>
                        lgm.LabelGroupName == uniqueServiceMatch.LabelGroupName
                        && lgm.Text?.FirstOrDefault()?.Text == uniqueServiceMatch.Text?.FirstOrDefault()?.Text);

                    if (exists)
                    {
                        continue;
                    }
                    
                    uniqueServiceMatchesNotInLabelGroupMatches.Add(uniqueServiceMatch);
                }

                labelGroupMatches.AddRange(uniqueServiceMatchesNotInLabelGroupMatches);
                
                unmatchedOrMoreWantedLabelLookups = GetUnmatchedOrMoreWantedLabels(
                    unmatchedOrMoreWantedLabelLookups,
                    labelGroupMatches,
                    false);
                    
                var labelsNotMatchedAtAll2 = GetUnmatchedOrMoreWantedLabels(
                    unmatchedOrMoreWantedLabelLookups,
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

            unmatchedOrMoreWantedLabelLookups = GetUnmatchedOrMoreWantedLabels(
                unmatchedOrMoreWantedLabelLookups,
                labelGroupMatches,
                false);
            
            var labelsNotMatchedAtAll3 = GetUnmatchedOrMoreWantedLabels(
                unmatchedOrMoreWantedLabelLookups,
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

        await SaveImageMetadataIfChangedAsync(imageMetadataChanged, pdfDocument, imagesMetadata, processRunId);
        noOcrDataExtractorService.Release(pdfDocument);

        returnResult.Matches = labelGroupMatches;
        return returnResult;      
    }

    private static bool IsPageScan(int imageWidth, int imageHeight)
    {
        const int minWidth = 1800;
        const int minHeightWhenWidthEnough = 100;

        var wideEnough = imageWidth >= minWidth && imageHeight >= minHeightWhenWidthEnough;

        if (wideEnough)
        {
            return true;
        }

        const int minHeight = 1800;
        const int minWidthWhenHeightEnough = 100;

        var tallEnough = imageHeight >= minHeight && imageWidth >= minWidthWhenHeightEnough;
        return tallEnough;
    }

    private static int GetSubResultCount(LabelGroupResult match)
    {
        var subResultCount = 0;

        foreach (var subResult in match.SubResults)
        {
            subResultCount += 1;

            foreach (var subResult2 in subResult.SubResults)
            {
                subResultCount += 1;
                                    
                foreach (var subResult3 in subResult2.SubResults)
                {
                    subResultCount += 1;
                                        
                    foreach (var subResult4 in subResult3.SubResults)
                    {
                        subResultCount += 1;
                                            
                        foreach (var subResult5 in subResult4.SubResults)
                        {
                            subResultCount += 1;
                                                
                            foreach (var subResult6 in subResult5.SubResults)
                            {
                                subResultCount += 1;
                            }
                        }
                    }
                }
            }
        }

        return subResultCount;
    }

    private static async Task<List<LabelGroupResult>> GetUniqueServiceMatchesAsync(Dictionary<IOcrDataExtractorService, List<LabelGroupResult>> serviceMatchesDict)
    {
        var uniqueServiceMatches = new List<LabelGroupResult>();

        foreach (var kvp in serviceMatchesDict.OrderBy(service => service.Key.HasDirectCost))
        {
            var serviceMatches = kvp.Value;

            foreach (var match in serviceMatches)
            {
                var alreadyFound = uniqueServiceMatches
                    .FirstOrDefault(x => x.LabelGroupName == match.LabelGroupName);

                if (alreadyFound == null)
                {
                    uniqueServiceMatches.Add(match);
                    continue;
                }

                string? newValue;
                
                switch (alreadyFound.MatchedLabel!.MultipleServiceMatchBehaviour)
                {
                    case MultipleServiceMatchBehaviour.UseAllUnique:
                        var multipleAlreadyFound = uniqueServiceMatches
                            .Where(x => x.LabelGroupName == match.LabelGroupName)
                            .ToList();

                        var existingValues = multipleAlreadyFound
                            .Select(af => string.Join(' ', af.Text!.Select(m => m.Text)))
                            .ToList();
                        
                        newValue = string.Join(' ', match.Text!.Select(m => m.Text));

                        if (!existingValues.Contains(newValue))
                        {
                            uniqueServiceMatches.Add(match);
                        }
                        else
                        {
                            var existingItem = uniqueServiceMatches
                                .First(x => x.LabelGroupName == match.LabelGroupName);
                            
                            existingItem.AlternativeMatches.Add(match);
                        }
                        
                        break;
                    case MultipleServiceMatchBehaviour.UseMostSubResultsUseLastServiceResultIfEqual:
                        var subResultCount = GetSubResultCount(match);
                        var alreadyFoundSubResultCount = GetSubResultCount(alreadyFound);

                        if (subResultCount >= alreadyFoundSubResultCount)
                        {
                            match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
                            alreadyFound.AlternativeMatches = [];
                            match.AlternativeMatches.Add(alreadyFound);

                            uniqueServiceMatches.Remove(alreadyFound);
                            uniqueServiceMatches.Add(match);
                        }
                        else
                        {
                            alreadyFound.AlternativeMatches.Add(match);
                        }
                        
                        break;
                    case MultipleServiceMatchBehaviour.UseLastServiceResult:
                        match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
                        alreadyFound.AlternativeMatches = [];
                        match.AlternativeMatches.Add(alreadyFound);

                        uniqueServiceMatches.Remove(alreadyFound);
                        uniqueServiceMatches.Add(match);
                        
                        break;
                    case MultipleServiceMatchBehaviour.UseFirstServiceResult:
                        alreadyFound.AlternativeMatches.Add(match);
                        match.AlternativeMatches = [];
                        
                        break;                        
                    case MultipleServiceMatchBehaviour.UseLongestUseLastServiceResultIfEqual:
                        var existingValue = string.Join(' ', alreadyFound.Text!.Select(m => m.Text));
                        newValue = string.Join(' ', match.Text!.Select(m => m.Text));

                        if (newValue.Length >= existingValue.Length)
                        {
                            match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
                            alreadyFound.AlternativeMatches = [];
                            match.AlternativeMatches.Add(alreadyFound);

                            uniqueServiceMatches.Remove(alreadyFound);
                            uniqueServiceMatches.Add(match);
                        }
                        else
                        {
                            alreadyFound.AlternativeMatches.Add(match);
                        }
                        
                        break;
                    case MultipleServiceMatchBehaviour.UseBestLicenceNumberUseLastServiceResultIfEqual:
                        var existingLicenceNumber = string.Join(' ', alreadyFound.Text!.Select(m => m.Text));
                        var existingDocumentLine = new DocumentLine
                        {
                            Columns = [
                                new()
                                {
                                    Text = existingLicenceNumber,
                                    Words = [new(
                                        existingLicenceNumber,
                                        null,
                                        new DocumentLineWordCoordinates(-1, -1, -1, -1),
                                        null)]
                                }
                            ]
                        };
                        
                        var existingValueNumberOfParts = existingLicenceNumber.Split('/').Length;
                        var existingValueNumberOfDigits = existingLicenceNumber.Count(char.IsDigit);
                        var existingValueLength = existingLicenceNumber.Length;
                        
                        var newLicenceNumber = string.Join(' ', match.Text!.Select(m => m.Text));
                        var newDocumentLine = new DocumentLine
                        {
                            Columns = [
                                new()
                                {
                                    Text = newLicenceNumber,
                                    Words = [new(
                                        newLicenceNumber,
                                        null,
                                        new DocumentLineWordCoordinates(-1, -1, -1, -1),
                                        null)]
                                }
                            ]
                        };
                        
                        var newValueNumberOfParts = newLicenceNumber.Split('/').Length;
                        var newValueNumberOfDigits = newLicenceNumber.Count(char.IsDigit);
                        var newValueLength = newLicenceNumber.Length;

                        if (newValueLength > existingValueLength
                            || newValueNumberOfDigits > existingValueNumberOfDigits
                            || newValueNumberOfParts > existingValueNumberOfParts)
                        {
                            match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
                            alreadyFound.AlternativeMatches = [];
                            match.AlternativeMatches.Add(alreadyFound);

                            uniqueServiceMatches.Remove(alreadyFound);
                            uniqueServiceMatches.Add(match);
                        }
                        else
                        {
                            alreadyFound.AlternativeMatches.Add(match);
                        }
                        
                        break;
                    case MultipleServiceMatchBehaviour.UseFullestDateUseLastServiceResultIfMultipleFull:
                        var existingDate = Date.GetDateFromString(alreadyFound.Text?.FirstOrDefault()?.Text);
                        var newDate = Date.GetDateFromString(match.Text?.FirstOrDefault()?.Text);

                        if (existingDate == null)
                        {
                            match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
                            alreadyFound.AlternativeMatches = [];
                            match.AlternativeMatches.Add(alreadyFound);

                            uniqueServiceMatches.Remove(alreadyFound);
                            uniqueServiceMatches.Add(match);
                        }
                        else if (newDate == null)
                        {
                            alreadyFound.AlternativeMatches.Add(match);
                        }
                        else
                        {
                            var existingDateHasDayField = existingDate.Value.Day > 1;
                            var existingDateIsPost1911 = existingDate.Value.Year >= 1911;
                            var existingDateYearHasLastDigitSet = existingDateIsPost1911 && int.Parse(existingDate.Value.Year.ToString()[3].ToString()) > 0;
                            
                            var newDateHasDayField = newDate.Value.Day > 1;
                            var newDateIsPost1911 = newDate.Value.Year >= 1911;
                            var newDateYearHasLastDigitSet = newDateIsPost1911 && int.Parse(newDate.Value.Year.ToString()[3].ToString()) > 0;
                            
                            if (newDateHasDayField && newDateIsPost1911
                                && (!existingDateHasDayField || !existingDateIsPost1911 || (newDateYearHasLastDigitSet && !existingDateYearHasLastDigitSet)))
                            {
                                match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
                                alreadyFound.AlternativeMatches = [];
                                match.AlternativeMatches.Add(alreadyFound);

                                uniqueServiceMatches.Remove(alreadyFound);
                                uniqueServiceMatches.Add(match);
                            }
                            else
                            {
                                alreadyFound.AlternativeMatches.Add(match);
                            }
                        }
                        
                        break;
                    default:
                        throw new Exception("MultipleServiceMatchBehaviour is not set, or not known");
                }
            }
        }

        return uniqueServiceMatches;
    }
    
    private async Task<ImageMetadata> LoadImageMetadataFromCacheAsync(PdfDocument pdfDocument, int processRunId)
    {
        var metaDataFileText = await cacheService.GetNoOcrImagesMetadataAsync(new NoOcrServiceMetadataCacheRequest
        {
            Filepath = pdfDocument.PdfFilePath,
            NoOcrServiceName = Name,
            ProcessRunId = processRunId
        });

        return JsonSerializer.Deserialize<ImageMetadata>(
            metaDataFileText!,
            JsonHelper.GetSerializerOptions())!;
    }
    
    private async Task<(ImageMetadata imageMetadata, bool imageMetadataChanged)>
        GetImageMetadataAsync(PdfDocument pdfDocument, int processRunId)
    {
        foreach (var page in pdfDocument.Pages)
        {
            await noOcrDataExtractorService.SavePageScreenshotIfDoesntExistAsync(
                outputService,
                pdfDocument,
                page.Number,
                Name,
                processRunId);
        }

        if (pdfDocument.FromCache)
        {
            return (await LoadImageMetadataFromCacheAsync(pdfDocument, processRunId), false);
        }

        var imagesMetadata = new ImageMetadata();
            
        foreach (var page in pdfDocument.Pages)
        {
            // TODO should use the interface (via a factory)
            var pageImageService = new PdfPigNoOcrPageService((UglyToad.PdfPig.Content.Page)page.PdfPigPage!);

            var metadataPage = new ImageMetadataPage
            {
                Number = page.Number,
                ImageReference = await outputService.GetPageScreenshotReferenceAsync(page.Number, Name, pdfDocument.PdfFilePath)
            };
            
            imagesMetadata.Pages.Add(metadataPage);
            var imageNumber = 1;
            
            foreach (var image in await pageImageService.GetImagesAsync())
            {
                var extension = await image.SaveImageBytesAsync(
                    pdfDocument.PdfFilePath,
                    imageNumber,
                    page.Number,
                    cacheService,
                    processRunId);

                if (extension == null)
                {
                    continue;
                }
                
                var imageReference = await cacheService.GetImageReferenceAsync(
                    page.Number,
                    imageNumber++,
                    pdfDocument.PdfFilePath,
                    extension);
                
                metadataPage.Images.Add(imageReference);
            }
        }

        return (imagesMetadata, true);
    }
    
    private static List<(string LabelGroupName, List<LabelToMatch> Labels)> GetUnmatchedOrMoreWantedLabels(
        List<(string LabelGroupName, List<LabelToMatch> Labels)> labels,
        List<LabelGroupResult> labelGroupMatches,
        bool onlyNotFoundAtAll)
    {
        return labels
            .Where(labelLookup =>
            {
                var doesntMatchAnyFound = labelGroupMatches.All(r =>
                    r.LabelGroupName != labelLookup.LabelGroupName);
                
                var fullLabel = labelGroupMatches.FirstOrDefault(lgm =>
                    lgm.MatchedLabel != null
                    && labelLookup.Labels.Any(l => l.Name == lgm.MatchedLabel.Name))?.MatchedLabel;

                var ifMultiplePreferLast = fullLabel?.Text?.FirstOrDefault()?.IfMultiplePreferLast ?? false;
                var ifMultiplePreferLongest = fullLabel?.Text?.FirstOrDefault()?.IfMultiplePreferLongest ?? false;                
                var canGoOverPageBoundary = fullLabel?.CanGoOverPageBoundary ?? false;
                var lookingForMultiple = fullLabel?.MultipleBehaviour
                    is MultipleBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                        or MultipleBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel;
                
                return doesntMatchAnyFound
                    || lookingForMultiple
                    || (!onlyNotFoundAtAll && (ifMultiplePreferLast || ifMultiplePreferLongest || canGoOverPageBoundary));
            })
            .ToList();
    }
    
    private async Task SaveImageMetadataIfChangedAsync(
        bool anyChanges,
        PdfDocument pdfDocument,
        ImageMetadata imagesMetadata,
        int processRunId)
    {
        if (!anyChanges)
        {
            return;
        }

        await cacheService.SaveNoOcrImagesMetadata(new NoOcrServiceMetadataCacheRequest
        {
            Filepath = pdfDocument.PdfFilePath,
            NoOcrServiceName = Name,
            ProcessRunId = processRunId
        }, imagesMetadata);
    }
    
    private async Task<List<LabelGroupResult>> GetLabelGroupMatchesAsync(
        List<DocumentLine> documentLines,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        bool isOcr,
        string serviceName,
        Dictionary<string, DmsFileData> licenceNumberMapping,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId)
    {
        var labelGroupMatches = new List<LabelGroupResult>();

        if (documentLines.Count == 0)
        {
            return labelGroupMatches;
        }

        var lines = StandardiseLines(documentLines);
        var wrappedLines = WrapLines(lines);
        
        foreach (var (labelGroupName, labels) in labelLookups)
        {
            if (AlreadyMatchedLabelGroup(labelGroupMatches, labelGroupName))
            {
                continue;
            }
            
            foreach (var label in labels)
            {
                var isRegularExpression = label.Text?.Any(text => text.IsRegularExpression) == true;
                
                if (label.Text?.Count > 0 && label.Text[0].Text == "Licence ")
                {
                    
                }
                
                if (!isRegularExpression && !LabelIsInDocument(label, documentLines))
                {
                    continue;
                }
                
                var labelGroupMatch = await FindLabelGroupMatchesInLinesAsync(
                    wrappedLines,
                    [label],
                    isOcr,
                    serviceName,
                    labelGroupName,
                    labelGroupMatches,
                    licenceNumberMapping,
                    previouslyParsedPaths,
                    regionCode,
                    processRunId);
                
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
        Dictionary<string, DmsFileData> licenceNumberMapping,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId)
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
                var stripped =  FormattingHelper.StripForComparison(licenceNumber.Text, regionCode);
                
                if (!licenceNumberMapping.TryGetValue(stripped!, out var dmsFileData))
                {
                    // TODO this should log a warning
                    continue;
                }
                
                var destinationFilePath = $"{pdfFolderPath}{dmsFileData.DestinationFileName}";
                
                if (previouslyParsedPaths.Contains(destinationFilePath))
                {
                    continue;
                }

                previouslyParsedPaths.Add(destinationFilePath);
                pathsToFetch.Add(destinationFilePath);
            }
        }

        foreach (var relatedFileName in pathsToFetch)
        {
            if (!File.Exists(relatedFileName))
            {
                continue;
            }
            
            var relatedFileMatches = await GetMatchesAsync(
                relatedFileName,
                new LookupConfiguration(
                    LabelConfiguration.GetLabels(),
                    licenceNumberMapping,
                    regionCode),
                previouslyParsedPaths,
                processRunId);

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

    private static bool NotMatchedAll(
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
        IReadOnlyList<DocumentLineWrapped> lines,
        IReadOnlyList<LabelToMatch> labels,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        IReadOnlyList<LabelGroupResult> siblingMatches,
        Dictionary<string, DmsFileData> licenceNumberMapping,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId)
    {
        var returnList = new List<LabelGroupResult>();

        var lineCount = -1;
        var totalLineCount = lines.Count;
        
        foreach (var line in lines)
        {
            var fullLine = line.Line;
            var breakLineLoop = false;
            
            foreach (var label in labels.Where(whereLabel => !whereLabel.Completed))
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
                    
                    var textBeforeAtAndAfterLabel = new List<TextAndLabel>();
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
                                licenceNumberMapping,
                                previouslyParsedPaths,
                                regionCode,
                                processRunId);

                            returnList.AddRange(linkedLicences);

                            partialLine = null;
                            continue;
                        }
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
                        nextLines ??= line.NextLines(lines, label);
                        var nextLine = nextLines.FirstOrDefault();
                        
                        if (!LabelMatchingHelper.LineContainsLabel(
                            partialLine,
                            nextLine,
                            fullLine!,
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
                        licenceNumberMapping = licenceNumberMapping,
                        pdfDataExtractorService = this,
                        previouslyParsedPaths = previouslyParsedPaths,
                        previousLines = previousLines,
                        nextLines = nextLines,
                        serviceName = serviceName,
                        siblingMatches = siblingMatches,
                        outputService = outputService,
                        cacheService = cacheService,
                        isSingleWord = matchedLabel.Format == SingleWord.Constant,
                        isUnitsLookup = matchedLabel.Format == Units.Constant,
                        line = partialLine,
                        lineForPosition = fullLine,
                        lineNumber = partialLine.LineNumber,
                        processRunId = processRunId,
                        regionCode = regionCode
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
        IReadOnlyList<DocumentLine> lines,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        Dictionary<string, DmsFileData> licenceMapping,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId)
    {
        var subResults = new List<LabelGroupResult>();
        
        if (label.SubLabels?.Count > 0)
        {
            var wrappedLines = WrapLines(lines);
            
            foreach (var subLabel in label.SubLabels)
            {
                if (subLabel.Remove == null && label.Remove != null)
                {
                    subLabel.Remove = label.Remove;
                }
                            
                var subLabelGroupMatch = await FindLabelGroupMatchesInLinesAsync(
                    wrappedLines,
                    [subLabel],
                    isOcr,
                    serviceName,
                    labelGroupName,
                    subResults,
                    licenceMapping,
                    previouslyParsedPaths,
                    regionCode,
                    processRunId);

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

    private static List<TextAndLabel> GetLineBeforeAtAndAfterText(
        DocumentLine line,
        LabelToMatch label)
    {
        var returnItems = new List<TextAndLabel>();

        var lineColumns = line.Columns.Select(c => c.Text).ToList();
        var lineText = line.Text;
        
        var isStartOfBlock = label.Text?.FirstOrDefault()?.Text
            .Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase) == true;

        if (label.Text == null || isStartOfBlock)
        {
            returnItems.Add(new TextAndLabel
            {
                ColumnsText = lineColumns,
                Label = label
            });

            return returnItems;
        }

        if (label.Text?.FirstOrDefault()?.IsRegularExpression == true &&
            label.Position == LabelPosition.ActuallyLabel)
        {
            var options = label.Text.First().RegularExpressionIsCaseInsensitive
                ? RegexOptions.IgnoreCase
                : RegexOptions.None;

            var matches = Regex.Matches(
                lineText,
                label.Text!.FirstOrDefault()!.Text,
                options);

            foreach (var match in matches.AsQueryable())
            {
                var regexValue = match.Value;
                var positionIndexOnLine = lineText.IndexOf(regexValue, StringComparison.Ordinal);

                if (positionIndexOnLine > 0)
                {
                    var previousChar = lineText[positionIndexOnLine - 1];

                    if (previousChar != ' ' && previousChar != ',' && previousChar != '.')
                    {
                        continue;
                    }
                }

                var valueStartPositionOnLine = lineText.IndexOf(
                    regexValue,
                    StringComparison.InvariantCultureIgnoreCase);
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
                    
                    returnItems.Add(new TextAndLabel
                    {
                        ColumnsText = beforeColumns,
                        Label = beforeLabel
                    });
                }
                
                returnItems.Add(new TextAndLabel
                {
                    ColumnsText = valueColumns,
                    Label = label
                });

                if (afterColumns.Count > 1 || !string.IsNullOrWhiteSpace(afterColumns.FirstOrDefault()))
                {
                    var afterLabel = label.Clone();
                    afterLabel.Position = LabelPosition.LabelIsBeforeTextToFind;

                    returnItems.Add(new TextAndLabel
                    {
                        ColumnsText = afterColumns,
                        Label = afterLabel
                    });
                }
            }

            return returnItems;
        }

        var labelTextPositionIndex = PositionConstants.PositionNotFound;
        string? matchedLabelText = null;

        foreach (var labelText in label.Text!)
        {
            var index = lineText.IndexOf(
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
            lineText[..labelTextPositionIndex], true, false);

        var textAtLabel = matchedLabelText;
        var textAfterLabel = FormattingHelper.TrimFormatting(
            lineText[(labelTextPositionIndex + matchedLabelText!.Length)..], false, false);

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
                ColumnsText = [textBeforeLabel.Trim()],
                Label = returnLabel
            });
        }

        if (!string.IsNullOrEmpty(textAtLabel) && label.IncludeStartLabelText)
        {
            var returnLabel = label.Clone();
            returnLabel.Position = LabelPosition.ActuallyLabel;

            returnItems.Add(new TextAndLabel
            {
                ColumnsText = [textAtLabel.Trim()],
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
                ColumnsText = [textAfterLabel.Trim()],
                Label = returnLabel
            });
        }

        return returnItems;
    }
    
    private static List<DocumentLine> StandardiseLines(IReadOnlyList<DocumentLine> lines)
    {
        var newLines = lines.ToList();

        foreach (var line in newLines)
        {
            FormattingHelper.Standardise(line.Columns);   
        }

        return newLines;
    }
    
    private static List<DocumentLineWrapped> WrapLines(IReadOnlyList<DocumentLine> lines)
    {
        return lines
            .Select((line, index) => new DocumentLineWrapped
            {
                Line = line,
                Index = index
            })
            .ToList();
    }
    
    private static bool LabelIsInDocument(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> lines)
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
                
                return (labelTextMatch, text);
            })
            .ToList();
        
        if (labelText.Any(tuple =>
            tuple.text.Equals(PositionConstants.StartOfBlockMarker, StringComparison.InvariantCultureIgnoreCase)))
        {
            return true;
        }
        
        return labelText.Any(tuple =>
        {
            return string.Join(',', lines.Select(line => line.Text)).Contains(tuple.text,
                StringComparison.InvariantCultureIgnoreCase);
        });
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