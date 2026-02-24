using System.Text.Json;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
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
    
    public async Task<PdfDocument> GetPdfDocumentAsync(
        string pdfFilePath,
        IOutputService outputService,
        ICacheService cacheService,
        INoOcrPdfDocumentService noOcrPdfDocumentService,
        int processRunId)
    {
        var metadata = await cacheService.GetMetadataAsync(pdfFilePath, Name, processRunId);
        var pdfDocument = new PdfDocument(
            pdfFilePath,
            metadata != null,
            outputService,
            noOcrPdfDocumentService);
        
        if (pdfDocument.FromCache)
        {
            pdfDocument.Pages = GetPages(metadata!.PagesMetadata!, pdfFilePath, outputService);
            pdfDocument.ImagesMetadata = metadata.ImageMetadata;
        
            pdfDocument.DocumentLines = await GetCachedTextLinesAsync(
                pdfDocument,
                metadata.PagesMetadata,
                metadata.AllDocumentLines!);
        
            return pdfDocument;
        }

        await PopulateImageDataAndDocumentLinesAsync(
            pdfDocument,
            cacheService,
            outputService,
            processRunId);
            
        // This one is just for auditing - not used for processing
        var saveAllPagesTextTask = outputService.SaveAllPagesTextAsync(
            pdfDocument.DocumentLines!,
            pdfFilePath,
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
        string pdfFilePath,
        IOutputService outputService)
    {
        var pageArray = ((JsonElement)pagesTextMetadata["pages"]).EnumerateArray().ToList();
        var pagesList = new List<PdfPage>();
            
        for (var pageNumber = 1; pageNumber <= pageArray.Count; pageNumber++)
        {
            var pageElement = pageArray[pageNumber - 1];
            var screenshotFilepaths = outputService.GetPageScreenshotReferences(
                pageNumber,
                Name,
                pdfFilePath);
            
            var pdfPage = new PdfPage
            {
                Number = pageNumber,
                NumberOfImages = pageElement.GetProperty("numberOfImages").GetInt32(),
                DigitalText = pageElement.GetProperty("text").GetString(),
                ScreenshotFilepaths = screenshotFilepaths
                    .Select(fp => fp.ImageReference)
                    .ToList()!
            };

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
        await cacheService.SaveNoOcrImagesMetadata(new NoOcrServiceMetadataCacheRequest
        {
            Filepath = pdfDocument.PdfFilePath,
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
            pdfDocument.PdfFilePath,
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
        
        var pagesMetadata = new List<Dictionary<string, object>>();
        var dtStart = DateTime.Now;

        var pages = pdfDocument.Pages;
        var getPagesDuration = DateTime.Now - dtStart;
        
        var processPageTasks = pages
            .Select(page => ProcessPageAsync(
                pdfDocument,
                page,
                cacheService,
                outputService,
                processRunId,
                pagesMetadata))
            .ToList();

        foreach (var processPageTask in processPageTasks)
        {
            documentLines.AddRange(await processPageTask);
        }
        
        var dtMetadataStart = DateTime.Now;
        await cacheService.SaveNoOcrPagesMetadataAsync(
            new NoOcrServiceMetadataCacheRequest
            {
                Filepath = pdfDocument.PdfFilePath,
                NoOcrServiceName = Name,
                ProcessRunId = processRunId
            },
            pagesMetadata);

        // Update line numbers, now in one big list
        var lineNumber = 0;
        documentLines.ForEach(documentLine => documentLine.LineNumber = lineNumber++);
        
        Console.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - Saving screenshots and getting document text lines took {(DateTime.Now - dtStart).TotalSeconds} seconds" +
            $" ({(DateTime.Now - dtMetadataStart).TotalMilliseconds}ms was for saving metadata, {getPagesDuration.TotalMilliseconds}ms was for getting pages in code)- {pdfDocument.PdfFilePath}");
        
        return documentLines;
    }

    private async Task<IReadOnlyList<DocumentLine>> ProcessPageAsync(
        PdfDocument pdfDocument,
        PdfPage page,
        ICacheService cacheService,
        IOutputService outputService,
        int processRunId,
        List<Dictionary<string, object>> pagesMetadata)
    {
        var dtStart = DateTime.Now;
        var size = await SavePageScreenshotAsync(outputService, pdfDocument, page.Number, Name, processRunId);
        
        Console.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - SavePageScreenshotAsync ({size / 1024.0 / 1024.0}mb) took {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilePath}");
        
        var pageRequest = new NoOcrServicePageCacheRequest
        {
            Filepath = pdfDocument.PdfFilePath,
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
            { "detailReference", cacheService.GetNoOcrPageReferenceAsync(pageRequest) }
        });

        if (FormattingHelper.IsPageEmpty(page.DigitalText))
        {
            await cacheService.SaveNoOcrPageTextLines(pageRequest, "[]");
            return [];
        }

        dtStart = DateTime.Now;
        var pdfPigPageLines = await GetPageLinesAsync((Page)page.InternalPage!.UnderlyingObject);
        Console.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - GetPageLinesAsync took {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilePath}");
        
        var pageLines = pdfPigPageLines.Select(MinimalTextBlock.FromPdfPigTextBlock).ToList();
        var serialisedPageLines = JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions());
        
        dtStart = DateTime.Now;
        await cacheService.SaveNoOcrPageTextLines(pageRequest, serialisedPageLines);
        Console.WriteLine(
            $"DEBUG - {nameof(PdfPigNoOcrDataExtractorService)} - SaveNoOcrPageTextLines ({serialisedPageLines.Length / 1024}kb) took {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilePath}");
        
        if (pdfPigPageLines.Count == 0)
        {
            return [];
        }
            
        var pageLinesFormatted = FormatPageLines(
            pageLines,
            page.Number);

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
            
        foreach (var page in pdfDocument.Pages)
        {
            // TODO should use the interface (via a factory)
            var pageImageService = new PdfPigNoOcrPageService(page.InternalPage!);

            var metadataPage = new ImageMetadataPage
            {
                Number = page.Number,
                ScreenshotReferences = outputService
                    .GetPageScreenshotReferences(page.Number, Name, pdfDocument.PdfFilePath)
                    .Select(sr => new ImageMetadataPageScreenshot
                    {
                        ImageReference = sr.ImageReference,
                        ProviderName = sr.ProviderName
                    })
                    .ToList()
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
                    extension,
                    Name);
                
                metadataPage.Images.Add(imageReference);
            }
        }

        return imagesMetadata;
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