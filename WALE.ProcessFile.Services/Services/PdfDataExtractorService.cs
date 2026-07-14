using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Methods;
using WALE.ProcessFile.Services.Models;
using LinkedLicence = WALE.ProcessFile.Services.Formats.LinkedLicence;

namespace WALE.ProcessFile.Services.Services;

public class PdfDataExtractorService(
    INoOcrDataExtractorService noOcrDataExtractorService,
    IEnumerable<IOcrDataExtractorService> ocrDataExtractorServices,
    ICacheService cacheService,
    IOutputService outputService,
    INoOcrPdfDocumentService noOcrPdfDocumentService,
    INoOcrAlternativePdfDocumentService noOcrAlternativePdfDocumentService,
    int id = -1) : IPdfDataExtractorService
{
    public int Id { get; set; } = id;
    public bool InUse { get; set; } = false;
    private string Name => noOcrPdfDocumentService.Name!;

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PathLocks = new();
    
    public async Task<MatchesResult> GetMatchesAsync(
        string pdfFileName,
        DmsFileData dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedFiles,
        int processRunId)
    {
        if (pdfFileName.Split('/').Length > 1)
        {
            Console.WriteLine($"WARNING - {nameof(PdfDataExtractorService)} - Pdf file name should not contain full path");
            pdfFileName = FileHelper.GetFilenameWithExtension(pdfFileName)!;
        }
        
        var dtStart = DateTime.Now;
        
        var pathLock = PathLocks.GetOrAdd(
            dmsDataForFile.FileId.ToString(),
            _ => new SemaphoreSlim(1, 1));
        
        await pathLock.WaitAsync();

        try
        {
            var lockWaitDuration = DateTime.Now.Subtract(dtStart);

            if (lockWaitDuration.TotalMilliseconds > 1000)
            {
                ConsoleHelper.WriteLine(
                    $"WARNING - {nameof(PdfDataExtractorService)} - Waited at lock for {lockWaitDuration.TotalMilliseconds}ms - {dmsDataForFile.FileId} {pdfFileName}");
            }

            return await GetMatchesInternalAsync(
                pdfFileName,
                dmsDataForFile,
                configuration,
                previouslyParsedFiles,
                processRunId);
        }
        finally
        {
            pathLock.Release();
        }
    }

    private async Task<MatchesResult> GetMatchesInternalAsync(
        string pdfFileName,
        DmsFileData? dmsDataForFile,
        LookupConfiguration configuration,
        List<string> previouslyParsedPaths,
        int processRunId)
    {
        ArgumentNullException.ThrowIfNull(dmsDataForFile);

        if (dmsDataForFile.FileId == Guid.Empty)
        {
            throw new Exception("FileId is empty");
        }
        
        var returnResult = new MatchesResult
        {
            Filename = pdfFileName,
            RegionCode = configuration.RegionId,
            ServicesUsed =
            [
                noOcrDataExtractorService.Name,
                GeneralConstants.DocnetExtractorServiceName
            ] // TODO, tidy this up
        };
        
        var dtStart = DateTime.Now;
        var additionalInformationStore = new Dictionary<string, object?>();
        
        var pdfDocument = await noOcrDataExtractorService.GetPdfDocumentAsync(
            pdfFileName,
            dmsDataForFile.FileId,
            outputService,
            cacheService,
            noOcrPdfDocumentService,
            noOcrAlternativePdfDocumentService,
            configuration,
            processRunId);

        if (pdfDocument == null)
        {
            returnResult.ErrorMessage = "Could not open pdf document";
            return returnResult;
        }
        
        var sizeKb = (pdfDocument.SizeBytes / 1024.0).ToString("0.0");
        var durationMs = (DateTime.Now - dtStart).TotalMilliseconds;
        
        if (pdfDocument.FromCache)
        {
            ConsoleHelper.WriteLine(
                $"DEBUG - {nameof(PdfDataExtractorService)} - Getting pdf document from cache. " +
                $"Cache size = {sizeKb}kb." +
                $"Took {durationMs}ms - {pdfDocument.PdfFilename}");
        }
        else
        {
            ConsoleHelper.WriteLine(
                $"DEBUG - {nameof(PdfDataExtractorService)} - Getting pdf document from s3. " +
                $"Size = {sizeKb}kb." +
                $"Took {durationMs}ms - {pdfDocument.PdfFilename}");            
        }
        
        if (pdfDocument.DocumentLines == null)
        {
            throw new Exception($"ERROR - {nameof(PdfDataExtractorService)} - TextLines hasn't been initialized");
        }
        
        if (pdfDocument.ImagesMetadata == null)
        {
            throw new Exception($"ERROR - {nameof(PdfDataExtractorService)} - ImagesMetadata hasn't been initialized");
        }
        
        dtStart = DateTime.Now;

        returnResult.NumberOfPages = pdfDocument.Pages.Count;
        returnResult.Pages = pdfDocument.Pages;
        
        var isOcr = false;
        
        var labelGroupMatches = await GetLabelGroupMatchesAsync(
            pdfDocument.DocumentLines,
            configuration.Labels,
            isOcr,
            noOcrDataExtractorService.Name,
            configuration.AllDmsData,
            previouslyParsedPaths,
            configuration.RegionId,
            processRunId,
            configuration,
            additionalInformationStore);

        ConsoleHelper.WriteLine(
            $"DEBUG - {nameof(PdfDataExtractorService)} - Getting digital text label matches took {(DateTime.Now - dtStart).TotalMilliseconds}ms" +
            $" - {pdfDocument.PdfFilename}");
        
        // De-dupe
        var newLabelGroupMatches = new List<LabelGroupResult>();

        foreach (var labelGroupMatch in labelGroupMatches)
        {
            var exists = newLabelGroupMatches.Any(lgm =>
                lgm.LabelGroupName == labelGroupMatch.LabelGroupName
                && DataHelper.GetFirstLineTextFromMatch(lgm) == DataHelper.GetFirstLineTextFromMatch(labelGroupMatch));

            if (exists)
            {
                continue;
            }
            
            newLabelGroupMatches.Add(labelGroupMatch);
        }

        labelGroupMatches = newLabelGroupMatches;
        dtStart = DateTime.Now;
        
        var allImagesInDocument = await cacheService.GetImagesAsync(
            new OcrServiceImageDataCacheRequest
            {
                FileId = dmsDataForFile.FileId,
                NoOcrServiceName = Name
            });

        ConsoleHelper.WriteLine(
            $"DEBUG - {nameof(PdfDataExtractorService)} - Getting all images in document metadata took {(DateTime.Now - dtStart).TotalMilliseconds}ms" +
            $" - {pdfDocument.PdfFilename}");
        
        var isLikelyTextFile = pdfDocument.DocumentLines.Count >= 100;
        var totalPagesToProcess = pdfDocument.ImagesMetadata!.Pages.Count;
        
        if (!isLikelyTextFile
            && returnResult.Pages.Count > configuration.MaxPagesToProcessWhenOcrNeeded)
        {
            totalPagesToProcess = configuration.MaxPagesToProcessWhenOcrNeeded;
        }
        
        // Some PDFs have a text component but are mainly scans (not sure how this has come about)
        // So we need to work out if it's predominately a text file (and there are no big images), we don't need to go off and do image lookups
        if (isLikelyTextFile)
        {
            // There are no images - we have finished with looking at text only
            if (allImagesInDocument.Count == 0)
            {
                returnResult.Matches = labelGroupMatches;
                return returnResult;
            }

            var anyImageLargeEnoughToBePageScan = false;

            const int maxPagesToDetermineIfScan = 4;

            var maxPagesToLookAt = totalPagesToProcess;
            if (maxPagesToLookAt > maxPagesToDetermineIfScan)
            {
                maxPagesToLookAt = maxPagesToDetermineIfScan;
            }

            for (var pageNumber = 1; pageNumber <= maxPagesToLookAt; pageNumber++)
            {
                var page = pdfDocument.ImagesMetadata.Pages
                    .Single(p => p.Number == pageNumber);
                
                for (var imageNumber = 1; imageNumber <= page.Images.Count; imageNumber++)
                {
                    var image = allImagesInDocument
                        .FirstOrDefault(i => i.pageNumber == pageNumber && i.imageNumber == imageNumber);

                    if (image == null)
                    {
                        ConsoleHelper.WriteLine($"WARNING - {nameof(PdfDataExtractorService)} - image not" +
                            $" found, P{page} I{imageNumber} {dmsDataForFile.FileId}");
                        
                        continue;
                    }
                    
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
        isOcr = true;

        if ((DateTime.Now - dtStart).TotalMilliseconds >= 1000)
        {
            ConsoleHelper.WriteLine(
                $"INFO - {nameof(PdfDataExtractorService)} - Checking digital text stuff took {(DateTime.Now - dtStart).TotalMilliseconds}ms" +
                $" - {pdfDocument.PdfFilename}");
        }

        var documentLines = new List<DocumentLine>();
        
        for (var pageNumber = 1; pageNumber <= totalPagesToProcess; pageNumber++)
        {
            dtStart = DateTime.Now;
            
            var page = pdfDocument.ImagesMetadata.Pages
                .Single(p => p.Number == pageNumber);
           
            var breakPageLoop = false;

            var pageImages = page.Images.ToList(); // They are ordered earlier
            var servicesUsed = new List<string>();
            
            if (pageImages.Count > 10)
            {
                ConsoleHelper.WriteLine($"INFO - Page {pageNumber} had more then 10 images, swapping to screenshot" +
                    $" - {pdfDocument.PdfFilename}");
                
                pageImages = page.ScreenshotReferences
                    .Select(sr => sr.ImageReference)
                    .ToList()!;

                foreach (var pageImage in pageImages)
                {
                    var extension = pageImage.Split('.').Last();
                    
                    allImagesInDocument.Insert(0, new ImageDetails
                    {
                        pageNumber = pageNumber,
                        imageNumber = 1,
                        extension = extension,
                        width = 2000,
                        height = 2000
                    });   
                }
            }

            for (var imageNumber = 1; imageNumber <= pageImages.Count; imageNumber++)
            {
                var imageReference = pageImages[imageNumber - 1];

                if (imageReference.Contains("-error-", StringComparison.InvariantCultureIgnoreCase))
                {
                    Console.WriteLine($"INFO - {nameof(PdfDataExtractorService)} - Skipping missing image {imageReference}");
                    continue;
                }
                
                var breakImageLoop = false;

                var serviceImageLines = new List<DocumentLine>();
                var serviceMatchesDict = new Dictionary<IOcrDataExtractorService, List<LabelGroupResult>>();
                
                foreach (var ocrService in ocrDataExtractorServices
                    .OrderBy(service => service.HasDirectCost))
                {
                    if (!servicesUsed.Contains(ocrService.Name))
                    {
                        servicesUsed.Add(ocrService.Name);
                    }
                    
                    if (!returnResult.ServicesUsed.Contains(ocrService.Name))
                    {
                        returnResult.ServicesUsed.Add(ocrService.Name);
                    }

                    try
                    {
                        serviceImageLines =
                            (await ocrService.GetTextLinesFromImageAsync(
                                imageReference,
                                pageNumber,
                                imageNumber,
                                pdfDocument,
                                processRunId,
                                Name)).ToList();
                    }
                    catch (Exception ex)
                    {
                        ConsoleHelper.WriteLine($"ERROR - {ocrService.Name} - {ex} - {imageReference}");
                        // TODO proper logging somewhere
                        
                        // Don't rethrow - just carry on with the other providers and pages
                        continue;
                    }
                    
                    // No lines found, no point processing that with the other services
                    if (serviceImageLines.Count == 0)
                    {
                        break;
                    }

                    var outputPage = returnResult.Pages
                        .Single(p => p.Number == page.Number);
                    var providers = outputPage.Providers;

                    if (providers.All(p => p.Provider != ocrService.Name))
                    {
                        providers.Add(new PdfPageProvider
                        {
                            Provider = ocrService.Name,
                            Text = serviceImageLines.Select(l => l.Text).ToList()
                        });
                    }
                    
                    if (DataHelper.LikelyMapPage(serviceImageLines, pageImages.Count))
                    {
                        outputPage.LikelyMapPage = true;
                        serviceImageLines = [];

                        break;
                    }
                    
                    var allLinesSoFar = documentLines.ToList();
                    allLinesSoFar.AddRange(serviceImageLines);
                    
                    var serviceMatches = await GetLabelGroupMatchesAsync(
                        allLinesSoFar,
                        unmatchedOrMoreWantedLabelLookups,
                        isOcr,
                        ocrService.Name,
                        configuration.AllDmsData,
                        previouslyParsedPaths,
                        configuration.RegionId,
                        processRunId,
                        configuration,
                        additionalInformationStore);
                    
                    serviceMatchesDict.Add(ocrService, serviceMatches);
                    var noMatchesFound = serviceMatches.Count == 0;
                    
                    if (noMatchesFound)
                    {
                        continue;
                    }
                    
                    foreach (var ocrResult in serviceMatches)
                    {
                        var matchedLabel = ocrResult.MatchedLabel!;
                        var ifMultiplePreferLast = matchedLabel.TextToMatch!.First().IfMultiplePreferLast;
                        var ifMultiplePreferLongest = matchedLabel.TextToMatch!.First().IfMultiplePreferLongest;

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
            
            ProfilePage(dtStart, pageNumber, pageImages.Count, pdfDocument, servicesUsed);
            
            if (breakPageLoop || labelsNotMatchedAtAll3.Count == 0)
            {
                break;
            }
        }
        
        noOcrDataExtractorService.Release(pdfDocument);

        returnResult.Matches = labelGroupMatches;
        returnResult.AdditionalInformation = additionalInformationStore;
        
        return returnResult;
    }

    private static void ProfilePage(
        DateTime dtStart,
        int pageNumber,
        int numberOfImages,
        PdfDocument pdfDocument,
        List<string> servicesUsed)
    {
        var duration = DateTime.Now - dtStart;
        
        ConsoleHelper.WriteLine($"INFO - {nameof(PdfDataExtractorService)} - Page number {pageNumber} ({numberOfImages} images) took {duration.TotalMilliseconds} milliseconds" +
            $". Services used {string.Join(", ", servicesUsed)} - {pdfDocument.PdfFilename}");
    }
    
    private static bool IsPageScan(int imageWidth, int imageHeight)
    {
        const int minWidth = 1800;
        const int minHeightWhenWidthEnough = 130;

        var wideEnough = imageWidth >= minWidth && imageHeight >= minHeightWhenWidthEnough;

        if (wideEnough)
        {
            return true;
        }

        const int minHeight = 1800;
        const int minWidthWhenHeightEnough = 130;

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

    private static void AddHighestConfidenceResult(
        LabelGroupResult match,
        LabelGroupResult alreadyFound,
        List<LabelGroupResult> uniqueServiceMatches)
    {
        var existingConfidence = alreadyFound.Text?.FirstOrDefault()?.OcrConfidence;
        var newConfidence = match.Text?.FirstOrDefault()?.OcrConfidence;

        if (newConfidence > existingConfidence)
        {
            match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
            alreadyFound.AlternativeMatches = [];
            match.AlternativeMatches.Add(alreadyFound);

            uniqueServiceMatches.Remove(alreadyFound);
            uniqueServiceMatches.Add(match);
                            
            return;
        }
                        
        alreadyFound.AlternativeMatches.Add(match);
        match.AlternativeMatches = [];
    }
    
    private static List<LabelGroupResult> GetUniqueServiceMatches(
        Dictionary<IOcrDataExtractorService, List<LabelGroupResult>> serviceMatchesDict)
    {
        var uniqueServiceMatches = new List<LabelGroupResult>();

        foreach (var kvp in serviceMatchesDict.OrderBy(service => service.Key.HasDirectCost))
        {
            var serviceMatches = kvp.Value;

            foreach (var match in serviceMatches)
            {
                var alreadyFound = uniqueServiceMatches
                    .FirstOrDefault(usm => usm.LabelGroupName == match.LabelGroupName);

                if (alreadyFound == null)
                {
                    uniqueServiceMatches.Add(match);
                    continue;
                }

                string? newValue;
                
                switch (alreadyFound.MatchedLabel!.MultipleServiceMatchBehaviour)
                {
                    case MultipleServiceMatchBehaviour.UseHighestOcrConfidence:
                        AddHighestConfidenceResult(match, alreadyFound, uniqueServiceMatches);
                        break;
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
                    case MultipleServiceMatchBehaviour.UseFullestDateUseHighestOcrConfidenceIfMultipleFull:
                        var existingDate1 = Date.GetDateFromString(alreadyFound.Text?.FirstOrDefault()?.Text);
                        var newDate1 = Date.GetDateFromString(match.Text?.FirstOrDefault()?.Text);

                        if (existingDate1 == null)
                        {
                            match.AlternativeMatches.AddRange(alreadyFound.AlternativeMatches);
                            alreadyFound.AlternativeMatches = [];
                            match.AlternativeMatches.Add(alreadyFound);

                            uniqueServiceMatches.Remove(alreadyFound);
                            uniqueServiceMatches.Add(match);
                        }
                        else if (newDate1 == null)
                        {
                            alreadyFound.AlternativeMatches.Add(match);
                        }
                        else
                        {
                            var existingDateHasDayField = existingDate1.Value.Day > 1;
                            var existingDateIsPost1911 = existingDate1.Value.Year >= 1911;
                            var existingDateYearHasLastDigitSet = existingDateIsPost1911 && int.Parse(existingDate1.Value.Year.ToString()[3].ToString()) > 0;
                            
                            var newDateHasDayField = newDate1.Value.Day > 1;
                            var newDateIsPost1911 = newDate1.Value.Year >= 1911;
                            var newDateYearHasLastDigitSet = newDateIsPost1911 && int.Parse(newDate1.Value.Year.ToString()[3].ToString()) > 0;
                            
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
                                AddHighestConfidenceResult(match, alreadyFound, uniqueServiceMatches);
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

                var ifMultiplePreferLast = fullLabel?.TextToMatch?.FirstOrDefault()?.IfMultiplePreferLast ?? false;
                var ifMultiplePreferLongest = fullLabel?.TextToMatch?.FirstOrDefault()?.IfMultiplePreferLongest ?? false;                
                var canGoOverPageBoundary = fullLabel?.CanGoOverPageBoundary ?? false;
                var lookingForMultiple = fullLabel?.MultipleMatchBehaviour
                    is MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithASingleValuePerLabel
                        or MultipleMatchBehaviour.FindMultipleInstancesOfLabelWithMultipleValuesPerLabel;
                
                return doesntMatchAnyFound
                    || lookingForMultiple
                    || (!onlyNotFoundAtAll && (ifMultiplePreferLast || ifMultiplePreferLongest || canGoOverPageBoundary));
            })
            .ToList();
    }
    
    private async Task<List<LabelGroupResult>> GetLabelGroupMatchesAsync(
        List<DocumentLine> documentLines,
        IReadOnlyList<(string LabelGroupName, List<LabelToMatch> Labels)> labelLookups,
        bool isOcr,
        string serviceName,
        Dictionary<string, DmsFileData> licenceNumberMapping,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        Dictionary<string, object?> additionalInformationStore)
    {
        var labelGroupMatches = new List<LabelGroupResult>();

        if (documentLines.Count == 0)
        {
            return labelGroupMatches;
        }

        var lines = StandardiseLines(documentLines);
        var wrappedLines = WrapLines(lines, false);
        
        foreach (var (labelGroupName, labels) in labelLookups)
        {
            if (AlreadyMatchedLabelGroup(labelGroupMatches, labelGroupName))
            {
                continue;
            }
            
            foreach (var label in labels)
            {
                var isRegularExpression = label.TextToMatch?.Any(text => text.IsRegularExpression) == true;
                
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
                    processRunId,
                    lookupConfiguration,
                    additionalInformationStore);
                
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
        List<string> previouslyParsedFiles,
        int regionCode,
        int processRunId,
        LookupConfiguration lookupConfiguration)
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
            
            if (!FormattingHelper.GetDmsFileData(
                licenceNumber.Text,
                regionCode,
                licenceNumberMapping,
                out var dmsFileData))
            {
                continue;
            }
            
            var destinationFilenames = dmsFileData!.DestinationFileName!;
                
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
            clonedConfig.AllDmsData = licenceNumberMapping;
            clonedConfig.RegionId = regionCode;
            
            FormattingHelper.GetDmsFileData(
                relatedLicenceNumber,
                regionCode,
                lookupConfiguration.AllDmsData,
                out var linkedDmsFileData);

            if (linkedDmsFileData == null)
            {
                ConsoleHelper.WriteLine(
                    $"INFO - {nameof(PdfDataExtractorService)} - ProcessLinkedLicenceAsync - excluding file as doesn't have file id set");
                
                break;
            }
            
            var relatedFileMatches = await GetMatchesAsync(
                relatedFileName,
                linkedDmsFileData,
                clonedConfig,
                previouslyParsedFiles,
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
        int processRunId,
        LookupConfiguration lookupConfiguration,
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
                                processRunId,
                                lookupConfiguration);

                            returnList.AddRange(linkedLicences);

                            partialLine = null;
                            continue;
                        }
                    }

                    if (FormattingHelper.IsLineEmpty(partialLine)
                        && label.TextToMatch?.Any(text =>
                            text.Text.Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase)) != true
                        && !(label.Position == LabelPosition.SplitAtLabel && lineCount == totalLineCount - 1))
                    {
                        partialLine = null;
                        continue;
                    }

                    TextToMatch? matchedStartText = null;
                    var labelCharPosition = 0;

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
                            out labelCharPosition))
                        {
                            partialLine = null;
                            continue;
                        }
                    }
                    // If there is no text, only possibilities
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

                    var lineForPosition = fullLine;
                    var partialLineT = partialLine.Clone();
                    
                    if (label.LimitTo == LimitTo.SameColumn)
                    {
                        var matchedText = matchedLabel.Text?.FirstOrDefault()?.Text;
                        var newColumns = new List<DocumentLineColumn>();
                        
                        foreach (var column in partialLineT.Columns)
                        {
                            if (string.IsNullOrEmpty(matchedText) || !column.Text.Contains(matchedText))
                            {
                                continue;
                            }
                            
                            newColumns.Add(column);
                            break;
                        }
                        
                        partialLineT.Columns = newColumns;
                        lineForPosition = partialLineT;

                        textBeforeAtAndAfterLabel = [ 
                            new TextAndLabel
                            {
                                ColumnsText = [partialLineT.Columns[0].Text],
                                Label = matchedLabel
                            }
                        ];
                    }
                    
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
                        line = partialLineT,
                        lineForPosition = lineForPosition,
                        lineNumber = partialLine.LineNumber,
                        processRunId = processRunId,
                        regionCode = regionCode,
                        lookupConfiguration = lookupConfiguration,
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
                                $"INFO - {nameof(PdfDataExtractorService)} - ProcessExpressionResultAsync ({request.label.Name}, {expression.Key}) took {(DateTime.Now - dtStart).TotalMilliseconds}ms");
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
            (LabelPosition.SplitAtLabel, Split.FunctionAsync, 0),
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

    public async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> lines,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        Dictionary<string, DmsFileData> licenceMapping,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        Dictionary<string, object?> additionalInformationStore)
    {
        var subResults = new List<LabelGroupResult>();
        
        if (label.SubLabels?.Count > 0)
        {
            var wrappedLines = WrapLines(lines, true);
            
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
                    processRunId,
                    lookupConfiguration,
                    additionalInformationStore);

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
                    subResult.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text != "[START_OF_BLOCK]");
                
                var anyDidStartAtStartOfBlock = group.Any(subResult =>
                    subResult.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text == "[START_OF_BLOCK]");

                if (anyDidntStartAtStartOfBlock && anyDidStartAtStartOfBlock)
                {
                    subResults = subResults
                        .Where(subResult => subResult.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text != "[START_OF_BLOCK]")
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
        
        var isStartOfBlock = label.TextToMatch?.FirstOrDefault()?.Text
            .Equals("[START_OF_BLOCK]", StringComparison.InvariantCultureIgnoreCase) == true;

        if (label.TextToMatch == null || isStartOfBlock)
        {
            returnItems.Add(new TextAndLabel
            {
                ColumnsText = lineColumns,
                Label = label
            });

            return returnItems;
        }

        if (label.TextToMatch?.FirstOrDefault()?.IsRegularExpression == true &&
            label.Position == LabelPosition.LabelIsActuallyResult)
        {
            var options = label.TextToMatch.First().RegularExpressionIsCaseInsensitive
                ? RegexOptions.IgnoreCase
                : RegexOptions.None;

            var matches = Regex.Matches(
                lineText,
                label.TextToMatch!.FirstOrDefault()!.Text,
                options);

            foreach (var match in matches.AsQueryable())
            {
                var regexValue = match.Value;
                var positionIndexOnLine = lineText.IndexOf(regexValue, StringComparison.Ordinal);

                if (positionIndexOnLine > 0)
                {
                    var previousChar = lineText[positionIndexOnLine - 1];
                    var firstChar = match.Value[0];
                    
                    if (previousChar != ' ' && previousChar != ',' && previousChar != '.'
                        && firstChar != ' ' && firstChar != ',' && firstChar != '.')
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

        foreach (var labelText in label.TextToMatch!)
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
                or LabelPosition.SplitAtLabel)
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
            returnLabel.Position = LabelPosition.LabelIsActuallyResult;

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
                or LabelPosition.SplitAtLabel)
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
    
    private static List<DocumentLineWrapped> WrapLines(IReadOnlyList<DocumentLine> lines, bool clone)
    {
        return lines
            .Select((line, index) => new DocumentLineWrapped
            {
                Line = line.Clone(),
                Index = index
            })
            .ToList();
    }
    
    private static bool LabelIsInDocument(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> lines)
    {
        var labelText = label.TextToMatch!
            .Select(labelTextMatch => labelTextMatch.Text
                .Replace(PositionConstants.EndOfLineMarker, string.Empty)
                .Replace(PositionConstants.EndOfColumnMarker, string.Empty))
            .ToList();
        
        if (labelText.Contains(PositionConstants.StartOfBlockMarker, StringComparer.InvariantCultureIgnoreCase))
        {
            return true;
        }

        var joinedLines = string.Join(',', lines.Select(line => line.Text));
        
        return labelText.Any(text => joinedLines.Contains(text,
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