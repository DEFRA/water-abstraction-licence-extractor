using System.Collections.Concurrent;
using System.Text.Json;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using WALE.ProcessFile.Core.Configuration;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.PdfPig.Helpers;
using WALE.ProcessFile.Services.PdfPig.Models;
using TextBlock = UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock;

namespace WALE.ProcessFile.Services.PdfPig;

public class PdfPigNoOcrDataExtractorService : INoOcrDataExtractorService
{
    public string Name => "PdfPig";
    private const int LineHeight = 9;
    
    public async Task<PdfDocument?> GetPdfDocumentAsync(
        string pdfFileName,
        Guid fileId,
        IOutputService outputService,
        ICacheService cacheService,
        INoOcrPdfDocumentService noOcrPdfDocumentService,
        INoOcrAlternativePdfDocumentService noOcrAlternativePdfDocumentService,
        LookupConfiguration configuration,
        int processRunId)
    {
        var dtStart = DateTime.Now;
        var metadata = await cacheService.GetMetadataAsync(fileId, Name, processRunId);
        var durationMs = (DateTime.Now - dtStart).TotalMilliseconds;
        var debug = false;

        if (debug)
        {
            ConsoleHelper.WriteLine(
                $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - Attempting to get pdf document from cache (API)" +
                $" took {durationMs}ms - {pdfFileName}");
        }
        
        var pdfDocument = new PdfDocument(
            pdfFileName,
            fileId,
            metadata != null,
            metadata?.SizeBytes ?? -1,
            outputService,
            noOcrPdfDocumentService,
            noOcrAlternativePdfDocumentService,
            configuration);
        
        if (pdfDocument.FromCache)
        {
            pdfDocument.Pages = GetPages(
                metadata!.PagesMetadata!,
                fileId,
                outputService,
                configuration.SkipFileWhenMoreThenPages);
            
            pdfDocument.ImagesMetadata = metadata.ImageMetadata;
        
            pdfDocument.DocumentLines = await GetCachedTextLinesAsync(
                pdfDocument,
                metadata.PagesMetadata,
                metadata.AllDocumentLines!);
        
            return pdfDocument;
        }

        if (!await pdfDocument.OpenInternalDocumentAsync())
        {
            return null;
        }

        await PopulateImageDataAndDocumentLinesAsync(
            pdfDocument,
            cacheService,
            outputService,
            processRunId);
            
        // This one is just for auditing - not used for processing
        var saveAllPagesTextTask = outputService.SaveAllPagesTextAsync(
            pdfDocument.DocumentLines!,
            fileId,
            Name,
            processRunId);

        var saveImageMetadataTask = SaveImageMetadataAsync(
            pdfDocument,
            pdfDocument.ImagesMetadata!,
            processRunId,
            cacheService);

        await Task.WhenAll(saveAllPagesTextTask, saveImageMetadataTask);
        return pdfDocument;
    }

    private List<PdfPage> GetPages(
        Dictionary<string, object> pagesTextMetadata,
        Guid fileId,
        IOutputService outputService,
        int skipFileWhenMoreThenPages)
    {
        var pageArray = ((JsonElement)pagesTextMetadata["pages"])
            .EnumerateArray()
            .ToList();

        if (pageArray.Count > skipFileWhenMoreThenPages)
        {
            throw new TooManyPagesException(
                "Too many pages in this file - it is being skipped",
                pageArray.Count);
        }
        
        var pagesList = new List<PdfPage>();
            
        for (var pageNumber = 1; pageNumber <= pageArray.Count; pageNumber++)
        {
            var pageElement = pageArray[pageNumber - 1];
            
            var screenshotFilepaths = outputService.GetPageScreenshotReferences(
                pageNumber,
                Name,
                fileId);
            
            var pdfPage = new PdfPage
            {
                Number = pageNumber,
                NumberOfImages = pageElement.GetProperty("numberOfImages").GetInt32(),
                DigitalText = pageElement.GetProperty("text").GetString(),
                ScreenshotFilepaths = screenshotFilepaths
                    .Select(fp => fp.ImageReference)
                    .ToList()!
            };
            
            if (pdfPage.NumberOfImages  > PdfDocument.SkipFileIfMoreThenImages)
            {
                throw new TooManyImagesException(
                    "Too many images in this file - it is being skipped",
                    pdfPage.NumberOfImages,
                    pageArray.Count);
            }

            var providers = screenshotFilepaths
                .Select(fp => fp.ProviderName)
                .ToList();

            foreach (var provider in providers)
            {
                pdfPage.Providers.Add(new PdfPageProvider
                {
                    Provider = provider,
                    Text = [pdfPage.DigitalText!]
                });                
            }
                
            pagesList.Add(pdfPage);
        }

        return pagesList;
    }

    private async Task PopulateImageDataAndDocumentLinesAsync(
        PdfDocument pdfDocument,
        ICacheService cacheService,
        IOutputService outputService,
        int processRunId)
    {
        var documentLinesTask = GetTextLinesFromPdfAndSaveScreenshotsPageTextLinesAndMetadataAsync(
            pdfDocument,
            cacheService,
            outputService,
            processRunId);
        
        var imagesMetadataTask = GetImageMetadataAndSaveImagesAsync(
            pdfDocument,
            processRunId,
            outputService,
            cacheService);
        
        pdfDocument.ImagesMetadata = await imagesMetadataTask;
        pdfDocument.DocumentLines = await documentLinesTask;
    }
    
    private async Task SaveImageMetadataAsync(
        PdfDocument pdfDocument,
        ImageMetadata imagesMetadata,
        int processRunId,
        ICacheService cacheService)
    {
        await cacheService.SaveNoOcrImagesMetadataAsync(
            new NoOcrServiceMetadataCacheRequest
            {
                FileId = pdfDocument.FileId,
                NoOcrServiceName = Name,
                ProcessRunId = processRunId
            }, imagesMetadata);
    }
    
    public Task<int> SavePageScreenshotAsync(
        IOutputService outputService,
        PdfDocument pdfDocument,
        int pageNumber,
        string pdfServiceName,
        int processRunId)
    {
        return outputService.SavePageScreenshotAsync(
            pdfDocument,
            pageNumber,
            pdfServiceName,
            pdfDocument.FileId,
            processRunId);
    }

    private Task<List<DocumentLine>> GetCachedTextLinesAsync(
        PdfDocument pdfDocument,
        Dictionary<string, object>? pagesTextMetadata,
        Dictionary<int, string> allPagesTextLines)
    {
        var documentLines = new List<DocumentLine>();

        if (!pdfDocument.FromCache || pagesTextMetadata == null)
        {
            throw new Exception("Cache doesn't have pages text metadata");
        }

        var pagesElement = (JsonElement)pagesTextMetadata["pages"];
        var pageCount = pagesElement.GetArrayLength();
        
        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var pageElement = pagesElement[pageNumber - 1];
            var numberOfImages = pageElement.GetProperty("numberOfImages").GetInt32();

            var fileText = allPagesTextLines[pageNumber];

            if (string.IsNullOrEmpty(fileText))
            {
                // TODO should not happen
                continue;
            }

            var pageLines = JsonSerializer.Deserialize<List<MinimalTextBlock>>(
                fileText,
                JsonHelper.GetSerializerOptions())!;
            
            var pageLinesTransformed = FormatPageLines(
                pageLines,
                pageNumber);

            if (DataHelper.LikelyMapPage(pageLinesTransformed, numberOfImages))
            {
                continue;
            }
            
            documentLines.AddRange(pageLinesTransformed);
        }
        
        // Update line numbers, now in one big list
        var lineNumber = 0;
        documentLines.ForEach(documentLine => documentLine.LineNumber = lineNumber++);
        
        return Task.FromResult(documentLines);
    }

    public async Task<List<DocumentLine>> GetTextLinesFromPdfAndSaveScreenshotsPageTextLinesAndMetadataAsync(
        PdfDocument pdfDocument,
        ICacheService cacheService,
        IOutputService outputService,
        int processRunId)
    {
        var documentLines = new List<DocumentLine>();
        
        var pagesMetadata = new ConcurrentBag<Dictionary<string, object>>();
        var dtStart = DateTime.Now;

        var pages = pdfDocument.Pages;
        var getPagesDuration = DateTime.Now - dtStart;

        var dtProcessPagesStart = DateTime.Now;
        
        var processPageTasks = new List<Task<IReadOnlyList<DocumentLine>>>();
        const int maxSimultaneousToProcess = 3;
        
        foreach (var page in pages)
        {
            processPageTasks.Add(
                ProcessPageAsync(
                    pdfDocument,
                    page,
                    cacheService,
                    outputService,
                    processRunId,
                    pagesMetadata)); // Careful - this is being updated - TODO redesign);
            
            if (processPageTasks.Count != maxSimultaneousToProcess)
            {
                continue;
            }
            
            while (processPageTasks.Count >= maxSimultaneousToProcess)
            {
                await Task.WhenAny(processPageTasks);
                var toRemoveList = new List<Task<IReadOnlyList<DocumentLine>>>();
                
                foreach (var processPageTask in processPageTasks)
                {
                    if (!processPageTask.IsCompleted)
                    {
                        continue;
                    }
                    
                    documentLines.AddRange(processPageTask.Result); 
                    toRemoveList.Add(processPageTask);
                }

                foreach (var toRemoveItem in toRemoveList)
                {
                    processPageTasks.Remove(toRemoveItem);
                }
            }
        }
        
        foreach (var processPageTask in processPageTasks)
        {
            var lines = await processPageTask;
            documentLines.AddRange(lines);
        }
        
        var processPagesDuration = DateTime.Now - dtProcessPagesStart;
        var dtMetadataStart = DateTime.Now;

        var pagesMetadataList = pagesMetadata
            .OrderBy(pm => pm.TryGetValue("number", out var numberObj)
                ? (int)numberObj
                : throw new Exception("Page number is missing"))
            .ToList();
        
        await cacheService.SaveNoOcrPagesMetadataAsync(
            new NoOcrServiceMetadataCacheRequest
            {
                FileId = pdfDocument.FileId,
                NoOcrServiceName = Name,
                ProcessRunId = processRunId
            },
            pagesMetadataList);

        // Update line numbers, now in one big list
        var lineNumber = 0;
        
        documentLines.ForEach(documentLine => documentLine.LineNumber = lineNumber++);
        
        ConsoleHelper.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - Saving screenshots and getting document text lines took {(DateTime.Now - dtStart).TotalSeconds} seconds" +
            $" ({(DateTime.Now - dtMetadataStart).TotalMilliseconds}ms was for saving metadata, " +
            $"{getPagesDuration.TotalMilliseconds}ms was for getting pages in code, " +
            $"{processPagesDuration.TotalMilliseconds}ms was for processing pages)- {pdfDocument.PdfFilename}");
        
        return documentLines;
    }

    private async Task<IReadOnlyList<DocumentLine>> ProcessPageAsync(
        PdfDocument pdfDocument,
        PdfPage page,
        ICacheService cacheService,
        IOutputService outputService,
        int processRunId,
        ConcurrentBag<Dictionary<string, object>> pagesMetadata)
    {
        var dtStart = DateTime.Now;
        var size = await SavePageScreenshotAsync(outputService, pdfDocument, page.Number, Name, processRunId);
        var roundedSizeMb = (size / 1024.0 / 1024.0).ToString("0.0");
        
        ConsoleHelper.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - SavePageScreenshotAsync P{page.Number} ({roundedSizeMb}mb) took {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilename}");
        
        var pageRequest = new NoOcrServicePageCacheRequest
        {
            FileId = pdfDocument.FileId,
            NoOcrServiceName = Name,
            PageNumber = page.Number,
            ProcessRunId = processRunId
        };
            
        var numberOfImages = page.NumberOfImages;
            
        pagesMetadata.Add(new Dictionary<string, object>
        {
            { "number", page.Number },
            { "numberOfImages", page.NumberOfImages },
            { "text", page.DigitalText! },
            { "detailReference", await cacheService.GetNoOcrPageReferenceAsync(pageRequest) }
        });

        if (FormattingHelper.IsPageEmpty(page.DigitalText))
        {
            await cacheService.SaveNoOcrPageTextLinesAsync(pageRequest, "[]");
            return [];
        }

        dtStart = DateTime.Now;
        var pdfPigPageLines = await GetPageLinesAsync((Page)page.InternalPage!.UnderlyingObject);
        ConsoleHelper.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - GetPageLinesAsync took {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilename}");
        
        var pageLines = pdfPigPageLines.Select(MinimalTextBlock.FromPdfPigTextBlock).ToList();
        var serialisedPageLines = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions());
        
        dtStart = DateTime.Now;
        await cacheService.SaveNoOcrPageTextLinesAsync(pageRequest, serialisedPageLines);
        
        ConsoleHelper.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - SaveNoOcrPageTextLines ({serialisedPageLines.Length / 1024}kb) took {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilename}");
        
        if (pdfPigPageLines.Count == 0)
        {
            return [];
        }
            
        dtStart = DateTime.Now;
        var pageLinesFormatted = FormatPageLines(
            pageLines,
            page.Number);

        ConsoleHelper.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - FormatPageLines took {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilename}");
        
        if (DataHelper.LikelyMapPage(pageLinesFormatted, numberOfImages))
        {
            return [];
        }
            
        return pageLinesFormatted;
    }
    
    private async Task<ImageMetadata>
        GetImageMetadataAndSaveImagesAsync(
            PdfDocument pdfDocument,
            int processRunId,
            IOutputService outputService,
            ICacheService cacheService)
    {
        var imagesMetadata = new ImageMetadata();

        const int maxSimultaneousToProcess = 3;
        var pageTasks = new List<Task<ImageMetadataPage>>();    
        
        foreach (var page in pdfDocument.Pages)
        {
            pageTasks.Add(GetPageMetadataAsync(
                page,
                pdfDocument,
                outputService,
                cacheService,
                processRunId));

            if (pageTasks.Count != maxSimultaneousToProcess)
            {
                continue;
            }
            
            while (pageTasks.Count >= maxSimultaneousToProcess)
            {
                await Task.WhenAny(pageTasks);
                var toRemoveList = new List<Task<ImageMetadataPage>>();
                
                foreach (var pageTask in pageTasks)
                {
                    if (!pageTask.IsCompleted)
                    {
                        continue;
                    }
                    
                    imagesMetadata.Pages.Add(await pageTask);
                    toRemoveList.Add(pageTask);
                }

                foreach (var toRemoveItem in toRemoveList)
                {
                    pageTasks.Remove(toRemoveItem);
                }
            }
        }
        
        foreach (var pageTask in pageTasks)
        {
            var page = await pageTask;
            imagesMetadata.Pages.Add(page);
        }

        imagesMetadata.Pages = imagesMetadata.Pages
            .OrderBy(p => p.Number)
            .ToList();
        
        return imagesMetadata;
    }

    private async Task<ImageMetadataPage> GetPageMetadataAsync(
        PdfPage page,
        PdfDocument pdfDocument,
        IOutputService outputService,
        ICacheService cacheService,
        int processRunId)
    {
        // TODO should use the interface (via a factory)
        var pageImageService = new PdfPigNoOcrPageService(page.InternalPage!);

        var metadataPage = new ImageMetadataPage
        {
            Number = page.Number,
            ScreenshotReferences = outputService
                .GetPageScreenshotReferences(page.Number, Name, pdfDocument.FileId)
                .Select(sr => new ImageMetadataPageScreenshot
                {
                    ImageReference = sr.ImageReference,
                    ProviderName = sr.ProviderName
                })
                .ToList()
        };

        var images = await pageImageService.GetImagesAsync();
        var imageSaveTasks = new List<Task<(string Extension, int ImageNumber)>>();
        
        const int maxSimultaneousToProcess = 3;
        var idx = 1;
        
        foreach (var image in images)
        {
            imageSaveTasks.Add(
                image.SaveImageBytesAsync(
                    pdfDocument.FileId,
                    idx++,
                    page.Number,
                    cacheService,
                    processRunId));
            
            if (imageSaveTasks.Count != maxSimultaneousToProcess)
            {
                continue;
            }
            
            while (imageSaveTasks.Count >= maxSimultaneousToProcess)
            {
                await Task.WhenAny(imageSaveTasks);
                var toRemoveList = new List<Task<(string Extension, int ImageNumber)>>();
                
                foreach (var imageSaveTask in imageSaveTasks)
                {
                    if (!imageSaveTask.IsCompleted)
                    {
                        continue;
                    }
                    
                    var (fileExtension, imageNumber) = await imageSaveTask;
                    var imageReference = await cacheService.GetImageReferenceAsync(
                        page.Number,
                        imageNumber,
                        pdfDocument.FileId,
                        fileExtension,
                        Name);
                
                    metadataPage.Images.Add(imageReference);
                    toRemoveList.Add(imageSaveTask);
                }

                foreach (var toRemoveItem in toRemoveList)
                {
                    imageSaveTasks.Remove(toRemoveItem);
                }
            }
        }
        
        foreach (var imageTask in imageSaveTasks)
        {
            var (fileExtension, imageNumber) = await imageTask;
            var imageReference = await cacheService.GetImageReferenceAsync(
                page.Number,
                imageNumber,
                pdfDocument.FileId,
                fileExtension,
                Name);
            
            metadataPage.Images.Add(imageReference);
        }
        
        metadataPage.Images = metadataPage.Images
            .OrderBy(im => im
                .Replace("-error-", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("-jpg-", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Replace("-png-", string.Empty, StringComparison.OrdinalIgnoreCase))
            .ToList();
        
        return metadataPage;
    }
    
    private static IReadOnlyList<DocumentLine> FormatPageLines(
        IReadOnlyList<MinimalTextBlock> pageLineBlocks,
        int pageNumber)
    {
        if (pageLineBlocks.Count == 0)
        {
            return [];
        }
        
        const int blankLineGap = 37;
        
        var lineNumber = 0;
        var previousWordLine = (MinimalWord?)null;
        
        var orderedPageWords = pageLineBlocks
            .SelectMany(textBlock => textBlock.TextLines)
            .SelectMany(textLine => textLine.Words)
            .OrderByDescending(word => LineSnappingHelper.RoundToNearestN(
                word.BoundingBox.Bottom,
                LineHeight,
                word.Text))
            .ThenBy(line => line.BoundingBox.CentroidX)
            .ToList();
        
        MinimalWord? previousWord = null;
        var lineIndex = 0;
        
        var returnList = orderedPageWords
            .GroupBy(word =>
            {
                previousWord ??= word;
                
                var yDiff =
                    LineSnappingHelper.CompensateForBelowTheLineCharactersOffset(
                        previousWord.Text,
                        previousWord.BoundingBox.Bottom)
                    - LineSnappingHelper.CompensateForBelowTheLineCharactersOffset(
                        word.Text,
                        word.BoundingBox.Bottom);
                
                if (yDiff >= LineHeight)
                {
                    lineIndex += 1;
                }

                previousWord = word;
                return lineIndex;
            })
            .SelectMany(lineWords =>
            {
                var orderedWords = lineWords.OrderBy(x => x.BoundingBox.Left).ToList();
                
                var resultList = new List<DocumentLine>();
                var firstLine = orderedWords.First();

                var verticalDistanceFromPreviousLine =
                    previousWordLine?.BoundingBox.Bottom
                    - firstLine.BoundingBox.Bottom;

                if (verticalDistanceFromPreviousLine >= blankLineGap)
                {
                    var documentLineToAdd = new DocumentLine(
                        lineNumber++,
                        pageNumber,
                        [],
                        firstLine.BoundingBox.Top,
                        firstLine.BoundingBox.Right,
                        firstLine.BoundingBox.Bottom,
                        firstLine.BoundingBox.Left);

                    resultList.Add(documentLineToAdd);
                }

                previousWordLine = firstLine;
                
                var columns = new List<DocumentLineColumn>
                {
                    new()
                };

                MinimalWord? previousWord2 = null;
                
                foreach (var word in orderedWords)
                {
                    previousWord2 ??= word;
                    
                    var xDiff = word.BoundingBox.Left - previousWord2.BoundingBox.Right;
                    
                    if (xDiff >= 18)
                    {
                        columns.Add(new DocumentLineColumn());
                    }

                    var columnToAddTo = columns.Last();
                    columnToAddTo.Words.Add(new DocumentLineWord(
                        word.Text,
                        null,
                        DocumentLineWordCoordinatesHelper.Convert(word.BoundingBox),
                        "Digital"
                    ));

                    previousWord2 = word;
                }

                var documentLine = new DocumentLine(
                    lineNumber++,
                    pageNumber,
                    columns,
                    firstLine.BoundingBox.Top,
                    firstLine.BoundingBox.Right,
                    firstLine.BoundingBox.Bottom,
                    firstLine.BoundingBox.Left);

                resultList.Add(documentLine);
                return resultList;
            })
        .ToList();

        AutoCorrectHelper.RemoveSpacesAroundSlashes(returnList);
        return returnList;
    }
    
    private static async Task<IReadOnlyList<TextBlock>> GetPageLinesAsync(Page page)
    {
        return await Task.Run(() => RecursiveXYCut
            .Instance
            .GetBlocks(page.GetWords())
            .ToList());
    }
    
    public void Release(PdfDocument pdfDocument)
    {
        pdfDocument.Dispose();
    }
}