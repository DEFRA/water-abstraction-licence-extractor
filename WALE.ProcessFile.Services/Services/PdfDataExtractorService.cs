using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Formats;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class PdfDataExtractorService(
    INoOcrDataExtractorService noOcrDataExtractorService,
    IEnumerable<IOcrDataExtractorService> ocrDataExtractorServices,
    ICacheService cacheService,
    IOutputService outputService,
    INoOcrPdfDocumentService noOcrPdfDocumentService,
    INoOcrAlternativePdfDocumentService noOcrAlternativePdfDocumentService,
    IMessageQueueService  apiMessageQueueService,
    int id = -1) : IPdfDataExtractorService
{
    public int Id { get; set; } = id;
    public bool InUse { get; set; } = false;
    private string Name => noOcrPdfDocumentService.Name!;
    
    public async Task<(bool StopExecution, bool? AlreadySaved, MatchesResult? Item)> GetMatchesAsync(
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

        if (configuration.UseLockExclusivity)
        {
            var (stopExecution, matchesResult) = await CheckExclusiveAccess(
                dmsDataForFile,
                configuration.RegionId,
                pdfFileName,
                processRunId,
                configuration.CurrentLockRetryCount,
                configuration.LockInProcess);

            if (stopExecution)
            {
                return (true, null, null);
            }

            if (matchesResult != null)
            {
                return (false, true, matchesResult);
            }

            ConsoleHelper.WriteLine($"INFO - {nameof(PdfDataExtractorService)} - Save stub matches result (took out lock) for {dmsDataForFile.FileId}");
            
            await outputService.SaveStubMatchesResultAsync(
                pdfFileName,
                dmsDataForFile.FileId,
                processRunId);
        }

        try
        {
            return (false, false, await GetMatchesInternalAsync(
                pdfFileName,
                dmsDataForFile.FileId,
                configuration,
                previouslyParsedFiles,
                processRunId));
        }
        catch (Exception ex)
        {
            await outputService.SaveErrorMatchesResultAsync(
                pdfFileName,
                dmsDataForFile.FileId,
                processRunId,
                ex.ToString());

            throw;
        }
    }

    public async Task SaveMatchResultAsync(MatchesResult matchesResult, Guid fileId, int processRunId)
    {
        var matchResultId = await outputService.SaveMatchResultAsync(
            matchesResult,
            fileId,
            processRunId);

        var dtStartSaveMatches = DateTime.Now;

        if (matchesResult.Matches == null)
        {
            return;
        }

        var matches = matchesResult.Matches
            .Select(match => (matchResultId, match.MatchedLabelName, match.LabelGroupName, match))
            .ToList();

        await outputService.SaveMatchesAsync(matches);

        var saveDuration = (DateTime.Now - dtStartSaveMatches).TotalMilliseconds;
        ConsoleHelper.WriteLine(
            $"INFO - {nameof(PdfDataExtractorService)} - Saved {matches.Count} matches {fileId} in {saveDuration}ms at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
    }

    private async Task<(bool ShouldStopExecution, MatchesResult? Item)> CheckExclusiveAccess(
        DmsFileData dmsDataForFile,
        int regionId,
        string pdfFileName,
        int processRunId,
        int currentLockRetryCount,
        bool lockInProcess)
    {
        const int maxLockRetries = 5;
        
        if (currentLockRetryCount > maxLockRetries)
        {
            return (true, null);
        }
        
        var existingLicenceInRun = await
            outputService.GetMatchesResultAsync(dmsDataForFile.FileId, processRunId);
        
        if (existingLicenceInRun == null)
        {
            return (false ,null);
        }
        
        if (existingLicenceInRun.Status != nameof(ScrapeStatus.InProgress))
        {
            return (false, existingLicenceInRun);
        }
        
        const int delayInSeconds = 5;        
        
        if (lockInProcess)
        {
            existingLicenceInRun = await
                outputService.GetMatchesResultAsync(dmsDataForFile.FileId, processRunId);

            ConsoleHelper.WriteLine($"INFO - {nameof(PdfDataExtractorService)} - Waiting for {dmsDataForFile.FileId}");
            
            const int maxWaitTimeSeconds = 50;
            var maxFinishDateTime = DateTime.Now.AddSeconds(maxWaitTimeSeconds);
            
            while (existingLicenceInRun?.Status == nameof(ScrapeStatus.InProgress)
                && DateTime.Now <= maxFinishDateTime)
            {
                await Task.Delay(delayInSeconds * 1000);
                ConsoleHelper.WriteLine($"INFO - {nameof(PdfDataExtractorService)} - Waiting again for {dmsDataForFile.FileId}");
                
                existingLicenceInRun = await
                    outputService.GetMatchesResultAsync(dmsDataForFile.FileId, processRunId);
            }

            if (existingLicenceInRun?.Status != nameof(ScrapeStatus.InProgress))
            {
                ConsoleHelper.WriteLine($"INFO - {nameof(PdfDataExtractorService)} - Lock now released for {dmsDataForFile.FileId}");
                return (false, existingLicenceInRun);
            }
            
            ConsoleHelper.WriteLine($"ERROR - {nameof(PdfDataExtractorService)} - Gave up waiting for lock to be released for {dmsDataForFile.FileId}");
            return (true, null);
        }
    
        const int inProcessDelayInMilliseconds = 1000;
        await Task.Delay(inProcessDelayInMilliseconds);
        
        await apiMessageQueueService.AddToFileProcessQueue(
            new FileProcessSingleRequest
            {
                DelayInSeconds = delayInSeconds,
                DestinationFileName = dmsDataForFile.DestinationFileName,
                DmsPath = dmsDataForFile.DmsPath,
                FileId = dmsDataForFile.FileId,
                FilePath = pdfFileName,
                PermitNumber = dmsDataForFile.PermitNumber,
                ProcessRunId = processRunId,
                RegionId = regionId,
                RequestedAt = DateTime.Now,
                LockRetryCount = currentLockRetryCount + 1
            });

        return (true, null);
    }

    private async Task<MatchesResult> GetMatchesInternalAsync(
        string pdfFileName,
        Guid fileId,
        LookupConfiguration configuration,
        List<string> previouslyParsedPaths,
        int processRunId)
    {
        if (fileId == Guid.Empty)
        {
            ConsoleHelper.WriteLine($"ERROR - {nameof(PdfDataExtractorService)} - File Id is empty for {pdfFileName}");
            throw new Exception("FileId is empty");
        }
        
        var returnResult = new MatchesResult
        {
            Filename = pdfFileName,
            RegionCode = configuration.RegionId,
            Status = nameof(ScrapeStatus.Ok),
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
            fileId,
            outputService,
            cacheService,
            noOcrPdfDocumentService,
            noOcrAlternativePdfDocumentService,
            configuration,
            processRunId);

        if (pdfDocument == null)
        {
            returnResult.ErrorMessage = "Could not open pdf document";
            ConsoleHelper.WriteLine($"ERROR - {nameof(PdfDataExtractorService)} - Could not open pdf document '{pdfFileName}'");
            
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
                FileId = fileId,
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
                            $" found, P{page} I{imageNumber} {fileId}");
                        
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
            var servicesUsed = new Dictionary<string, double>();
            
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

                if (imageReference.Contains("-error-", StringComparison.OrdinalIgnoreCase))
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
                    servicesUsed.TryAdd(ocrService.Name, 0);
                    var serviceStartTimeUtc = DateTime.UtcNow;
                    
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

                        var serviceDurationMs = (DateTime.UtcNow - serviceStartTimeUtc).TotalMilliseconds;
                        servicesUsed[ocrService.Name] += serviceDurationMs;
                        
                        // Don't rethrow - just carry on with the other providers and pages
                        continue;
                    }
                    
                    // No lines found, no point processing that with the other services
                    if (serviceImageLines.Count == 0)
                    {
                        var serviceDurationMs = (DateTime.UtcNow - serviceStartTimeUtc).TotalMilliseconds;
                        servicesUsed[ocrService.Name] += serviceDurationMs;
                        
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

                        var serviceDurationMs = (DateTime.UtcNow - serviceStartTimeUtc).TotalMilliseconds;
                        servicesUsed[ocrService.Name] += serviceDurationMs;
                        
                        break;
                    }
                    
                    var allLinesSoFar = documentLines.ToList();
                    allLinesSoFar.AddRange(serviceImageLines);
                    
                    var serviceMatches = await GetLabelGroupMatchesAsync(
                        allLinesSoFar,
                        unmatchedOrMoreWantedLabelLookups,
                        isOcr,
                        ocrService.Name,
                        previouslyParsedPaths,
                        configuration.RegionId,
                        processRunId,
                        configuration,
                        additionalInformationStore);
                    
                    serviceMatchesDict.Add(ocrService, serviceMatches);
                    var noMatchesFound = serviceMatches.Count == 0;
                    
                    if (noMatchesFound)
                    {
                        var serviceDurationMs = (DateTime.UtcNow - serviceStartTimeUtc).TotalMilliseconds;
                        servicesUsed[ocrService.Name] += serviceDurationMs;
                        
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
                                    .OrderBy(x => ifMultiplePreferLast ? ((x.LabelStartPageNumber * 100) + x.LabelStartLineNumber) : x.Text?.Count)
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

                        var serviceDurationMs = (DateTime.UtcNow - serviceStartTimeUtc).TotalMilliseconds;
                        servicesUsed[ocrService.Name] += serviceDurationMs;
                        
                        break;
                    }
                    
                    var serviceDurationMs1 = (DateTime.UtcNow - serviceStartTimeUtc).TotalMilliseconds;
                    servicesUsed[ocrService.Name] += serviceDurationMs1;
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
        Dictionary<string, double> servicesUsed)
    {
        var duration = DateTime.Now - dtStart;
        var servicesUsedStr = servicesUsed.Select(su => $"{su.Key} ({su.Value}ms)");
        
        ConsoleHelper.WriteLine($"INFO - {nameof(PdfDataExtractorService)} - Page number {pageNumber} ({numberOfImages} images) took {duration.TotalMilliseconds} milliseconds" +
            $". Services used {string.Join(", ", servicesUsedStr)} - {pdfDocument.PdfFilename}");
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
        var wrappedLines = DocumentLineWrapped.WrapLines(lines, false);
        var joinedLines = string.Join(',', lines.Select(line => line.Text));
        var documentLineService = new DocumentLineService(lines);
        
        foreach (var (labelGroupName, labels) in labelLookups)
        {
            if (AlreadyMatchedLabelGroup(labelGroupMatches, labelGroupName))
            {
                continue;
            }
            
            foreach (var label in labels)
            {
                var isRegularExpression = label.TextToMatch?.Any(text => text.Regex != null) == true;
                
                if (!isRegularExpression && !LabelIsInDocument(label, joinedLines))
                {
                    continue;
                }
                
                var labelGroupMatch =
                    await FindLabelGroupMatchesHelper.FindLabelGroupMatchesInLinesAsync(
                        wrappedLines,
                        [label],
                        isOcr,
                        serviceName,
                        labelGroupName,
                        labelGroupMatches,
                        previouslyParsedPaths,
                        regionCode,
                        processRunId,
                        lookupConfiguration,
                        this,
                        documentLineService,
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

    public async Task<List<LabelGroupResult>> ProcessSubLabelsAsync(
        LabelToMatch label,
        IReadOnlyList<DocumentLine> lines,
        bool isOcr,
        string? serviceName,
        string labelGroupName,
        List<string> previouslyParsedPaths,
        int regionCode,
        int processRunId,
        LookupConfiguration lookupConfiguration,
        IDocumentLineService documentLineService,
        Dictionary<string, object?> additionalInformationStore)
    {
        var subResults = new List<LabelGroupResult>();
        
        if (label.SubLabels?.Count > 0)
        {
            var wrappedLines = DocumentLineWrapped.WrapLines(lines, true);
            
            foreach (var subLabel in label.SubLabels)
            {
                var instanceWrappedLines = wrappedLines;
                
                if (false && subLabel is { PreviousLinesToFetch: > 0, Name: "DateOnly" })
                {
                    instanceWrappedLines = instanceWrappedLines.ToList();
                    var thisLine = lines[0];
                        
                    var startPageNumber = thisLine.PageNumber;
                    var endPageNumber = startPageNumber;

                    var endLineNumber = thisLine.LineNumber - 1;
                    var startLineNumber = thisLine.LineNumber - subLabel.PreviousLinesToFetch;

                    var extraLines = documentLineService.GetDocumentLines(
                        startPageNumber,
                        startLineNumber,
                        endPageNumber,
                        endLineNumber);

                    extraLines = extraLines
                        .Where(l =>
                            !(l.PageNumber == thisLine.PageNumber && l.LineNumber == thisLine.LineNumber))
                        .OrderBy(l => l.PageNumber)
                        .ThenBy(l => l.LineNumber)
                        .ToList();

                    var newLines = extraLines.ToList();
                    newLines.AddRange(instanceWrappedLines.Select(wl => wl.Line)!);
                        
                    instanceWrappedLines = DocumentLineWrapped.WrapLines(newLines, true);
                }

                if (subLabel.Remove == null && label.Remove != null)
                {
                    subLabel.Remove = label.Remove;
                }
 
                var subLabelGroupMatch =
                    await FindLabelGroupMatchesHelper.FindLabelGroupMatchesInLinesAsync(
                        instanceWrappedLines,
                        [subLabel],
                        isOcr,
                        serviceName,
                        labelGroupName,
                        subResults,
                        previouslyParsedPaths,
                        regionCode,
                        processRunId,
                        lookupConfiguration,
                        this,
                        documentLineService,
                        additionalInformationStore);

                if (subLabelGroupMatch.Count > 0)
                {
                    subResults.AddRange(subLabelGroupMatch);
                }
            }
        }
        
        var groups = subResults
            .GroupBy(x => x.MatchedLabel!.Name)
            .ToList();

        var subResultsToKeep = new List<LabelGroupResult>();
        
        foreach (var groupLoop in groups)
        {
            var group = groupLoop.ToList();

            if (group.Count == 1)
            {
                subResultsToKeep.Add(group[0]);
                continue;
            }

            var groupLabel = group.First().MatchedLabel!;
            
            if (groupLabel.DeDuplicateResults)
            {
                // De-dupe exact text matches
                group = group
                    .GroupBy(g =>
                    {
                        if (g.Text == null)
                        {
                            return string.Empty;
                        }

                        var text = string.Join(string.Empty, g.Text!.Select(t => t.Text));
                        return text;
                    })
                    .Select(g => g.Last())
                    .ToList();
            }
            
            if (!groupLabel.RemoveStartOfBlockSectionsWhenMultiple)
            {
                subResultsToKeep.AddRange(group);
                continue;
            }
            
            // If we found some with an implicit start label, then we don't want others
            // we found with a start of block
            
            var anyDidntStartAtStartOfBlock = group.Any(subResult =>
                subResult.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text != "[START_OF_BLOCK]");
            
            var anyDidStartAtStartOfBlock = group.Any(subResult =>
                subResult.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text == "[START_OF_BLOCK]");

            if (anyDidntStartAtStartOfBlock && anyDidStartAtStartOfBlock)
            {
                var newGroupSubResults = group
                    .Where(subResult => subResult.MatchedLabel?.TextToMatch?.FirstOrDefault()?.Text != "[START_OF_BLOCK]")
                    .ToList();
                
                subResultsToKeep.AddRange(newGroupSubResults);
                continue;
            }
            
            subResultsToKeep.AddRange(group);
        }

        return subResultsToKeep;
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
    
    private static bool LabelIsInDocument(
        LabelToMatch label,
        string joinedLines)
    {
        var labelText = label.TextToMatch!
            .Select(labelTextMatch => labelTextMatch.Text
                .Replace(PositionConstants.EndOfLineMarker, string.Empty)
                .Replace(PositionConstants.EndOfColumnMarker, string.Empty))
            .ToList();
        
        if (labelText.Contains(PositionConstants.StartOfBlockMarker, StringComparer.OrdinalIgnoreCase))
        {
            return true;
        }
        
        return labelText.Any(text => joinedLines.Contains(text,
            StringComparison.OrdinalIgnoreCase));
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