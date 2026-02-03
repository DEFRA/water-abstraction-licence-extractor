using System.Text.Json;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Helpers;
using TextBlock = UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrDataExtractorService : INoOcrDataExtractorService
{
    public string Name => "PdfPig";
    private const int LineHeight = 9;
    
    public async Task<PdfDocument> GetPdfDocumentAsync(
        string pdfFilePath,
        IOutputService outputService,
        ICacheService cacheService,
        int processRunId)
    {
        var request = new NoOcrServiceMetadataCacheRequest
        {
            Filepath = pdfFilePath,
            NoOcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        var metadataFileText = await cacheService.GetNoOcrPagesMetadataAsync(request);
        var pdfDocument = new PdfDocument(
            pdfFilePath,
            !string.IsNullOrEmpty(metadataFileText),
            outputService);
        
        if (!pdfDocument.FromCache)
        {
            return pdfDocument;
        }
        
        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
            metadataFileText!,
            JsonHelper.GetSerializerOptions())!;

        var pageArray = ((JsonElement)metadata["pages"]).EnumerateArray().ToList();
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

        pdfDocument.Pages = pagesList;
        return pdfDocument;
    }
    
    public Task SavePageScreenshotIfDoesntExistAsync(
        IOutputService outputService,
        PdfDocument pdfDocument,
        int pageNumber,
        string pdfServiceName,
        int processRunId)
    {
        return outputService.SavePageScreenshotIfDoesntExistAsync(
            pdfDocument,
            pageNumber,
            pdfServiceName,
            pdfDocument.PdfFilePath,
            processRunId);
    }

    public async Task<List<DocumentLine>> GetTextLinesFromPdfAsync(
        PdfDocument pdfDocument,
        ICacheService cacheService,
        int processRunId)
    {
        var dtStart = DateTime.Now;
        var documentLines = new List<DocumentLine>();

        var metadataRequest = new NoOcrServiceMetadataCacheRequest
        {
            Filepath = pdfDocument.PdfFilePath,
            NoOcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        if (pdfDocument.FromCache)
        {
            var metaDataFileText = await cacheService.GetNoOcrPagesMetadataAsync(metadataRequest);
            
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                metaDataFileText!,
                JsonHelper.GetSerializerOptions());

            var pagesElement = (JsonElement)metadata!["pages"];
            var pageCount = pagesElement.GetArrayLength();
            
            for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
            {
                dtStart = DateTime.Now;
                
                var pageElement = pagesElement[pageNumber - 1];
                var numberOfImages = pageElement.GetProperty("numberOfImages").GetInt32();
                
                var pageRequest = new NoOcrServicePageCacheRequest
                {
                    Filepath = pdfDocument.PdfFilePath,
                    NoOcrServiceName = Name,
                    PageNumber = pageNumber,
                    ProcessRunId = processRunId
                };

                var fileText = await cacheService.GetNoOcrPageTextLinesAsync(pageRequest);

                if (string.IsNullOrEmpty(fileText))
                {
                    // TODO should not happen
                    continue;
                }

                List<TextBlock> pageLines = [];
                
                Console.WriteLine($"Read {Name} text file page {pageNumber} in {(DateTime.Now - dtStart).TotalSeconds}" +
                    $" seconds - {pdfDocument.PdfFilePath}");
                
                var cachedTextBlocks = JsonSerializer.Deserialize<List<Models.PdfPig.DeserialisableTextBlock>>(
                    fileText,
                    JsonHelper.GetSerializerOptions())!;
                
                pageLines.AddRange(cachedTextBlocks.Select(
                    cachedTextBlock => cachedTextBlock.ToPdfPigTextBlock()));
                
                var pageLinesTransformed = FormatPageLines(
                    pageLines,
                    pageNumber);

                if (DataHelper.LikelyMapPage(pageLinesTransformed, numberOfImages))
                {
                    continue;
                }
                
                documentLines.AddRange(pageLinesTransformed);
            }
        }
        else
        {
            Console.WriteLine(
                $"Read {Name} document in {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfDocument.PdfFilePath}");
            
            var pagesMetadata = new List<Dictionary<string, object>>();
            
            foreach (var page in pdfDocument.Pages)
            {
                var pageRequest = new NoOcrServicePageCacheRequest
                {
                    Filepath = pdfDocument.PdfFilePath,
                    NoOcrServiceName = Name,
                    PageNumber = page.Number,
                    ProcessRunId = processRunId
                };

                var numberOfImages = page.NumberOfImages;
                var fileText = await cacheService.GetNoOcrPageTextLinesAsync(pageRequest);
                
                pagesMetadata.Add(new Dictionary<string, object>
                {
                    { "number", page.Number },
                    { "numberOfImages", page.NumberOfImages },
                    { "text", page.DigitalText! },
                    { "detailReference", cacheService.GetNoOcrPageReferenceAsync(pageRequest) },
                });

                List<TextBlock> pageLines = [];

                var fromCache = pdfDocument.FromCache && !string.IsNullOrEmpty(fileText);
                
                if (fromCache)
                {
                    dtStart = DateTime.Now;

                    Console.WriteLine(
                        $"Read {Name} text file page {page.Number} in {(DateTime.Now - dtStart).TotalSeconds} seconds");

                    var cachedTextBlocks =
                        JsonSerializer.Deserialize<List<Models.PdfPig.DeserialisableTextBlock>>(
                            fileText!,
                            JsonHelper.GetSerializerOptions())!;

                    pageLines.AddRange(cachedTextBlocks.Select(
                        cachedTextBlock => cachedTextBlock.ToPdfPigTextBlock()));

                    var pageLinesTransformed = FormatPageLines(
                        pageLines,
                        page.Number);
                    
                    if (DataHelper.LikelyMapPage(pageLinesTransformed, numberOfImages))
                    {
                        continue;
                    }
                    
                    documentLines.AddRange(pageLinesTransformed);
                    continue;
                }
                
                if (FormattingHelper.IsPageEmpty(page.DigitalText))
                {
                    await cacheService.SaveNoOcrPageTextLines(pageRequest, []);
                    continue;
                }

                pageLines.AddRange(await GetPageLinesAsync((Page)page.PdfPigPage!));
                await cacheService.SaveNoOcrPageTextLines(pageRequest, pageLines);
                
                if (pageLines.Count == 0)
                {
                    continue;
                }
                
                var pageLinesFormatted = FormatPageLines(
                    pageLines,
                    page.Number);

                if (DataHelper.LikelyMapPage(pageLinesFormatted, numberOfImages))
                {
                    continue;
                }
                
                documentLines.AddRange(pageLinesFormatted);
            }
            
            await cacheService.SaveNoOcrPagesMetadata(metadataRequest, pagesMetadata);
        }

        // Update line numbers, now in one big list
        var lineNumber = 0;
        documentLines.ForEach(documentLine => documentLine.LineNumber = lineNumber++);
        
        Console.WriteLine(
            $"Getting document text lines took {(DateTime.Now - dtStart).TotalSeconds} seconds" +
            $" - {pdfDocument.PdfFilePath}");
        
        return documentLines;
    }
    
    private static IReadOnlyList<DocumentLine> FormatPageLines(
        IReadOnlyList<TextBlock> pageLineBlocks,
        int pageNumber)
    {
        if (pageLineBlocks.Count == 0)
        {
            return [];
        }
        
        const int blankLineGap = 37;
        
        var lineNumber = 0;
        var previousWordLine = (Word?)null;
        
        var orderedPageWords = pageLineBlocks
            .SelectMany(textBlock => textBlock.TextLines)
            .SelectMany(textLine => textLine.Words)
            .OrderByDescending(word => LineSnappingHelper.RoundToNearestN(
                word.BoundingBox.Bottom,
                LineHeight,
                word.Text))
            .ThenBy(line => line.BoundingBox.Centroid.X)
            .ToList();
        
        Word? previousWord = null;
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
                        [new(string.Empty, [])],
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

                Word? previousWord2 = null;
                
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

                foreach (var column in columns)
                {
                    column.Text = string.Join(' ', column.Words.Select(w => w.Text));
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