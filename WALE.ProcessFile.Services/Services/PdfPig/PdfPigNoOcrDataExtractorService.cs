using System.Text.Json;
using SkiaSharp;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.Graphics.Colors;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using TextBlock = UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock;
using PdfDocument = WALE.ProcessFile.Services.Models.PdfDocument;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrDataExtractorService : INoOcrDataExtractorService
{
    public string Name => "PdfPig";
    private const int LineHeight = 9;
    
    public async Task<PdfDocument> GetPdfDocumentAsync(
        string pdfFilePath,
        string outputFolder,
        string cacheFolder)
    {
        var txtCacheFolder = $"{cacheFolder.Replace("//", "/")}/{Name}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{txtCacheFolder}/{PositionConstants.CacheMetadataFilename}";
        var existsInCache = File.Exists(metadataFilename);
        var pdfDocument = new PdfDocument(pdfFilePath, outputFolder, cacheFolder, existsInCache);

        if (!existsInCache)
        {
            return pdfDocument;
        }
        
        var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
        var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
            metaDataFileText,
            JsonHelper.GetSerializerOptions())!;

        var pageArray = ((JsonElement)metadata["pages"]).EnumerateArray().ToList();
        var pagesList = new List<PdfPage>();
            
        for (var pageNumber = 1; pageNumber <= pageArray.Count; pageNumber++)
        {
            var pageElement = pageArray[pageNumber - 1];
            var pdfPage = new PdfPage
            {
                Number = pageNumber,
                NumberOfImages = pageElement.GetProperty("numberOfImages").GetInt32(),
                Text = pageElement.GetProperty("text").GetString()
            };

            pdfPage.ImageFilepath = $"{outputFolder}/{pdfPage.GetImageFilepath(Name)}";
            pdfPage.Providers.Add(new PdfPageProvider
            {
                Provider = Name,
                Text = [pdfPage.Text!]
            });
                
            pagesList.Add(pdfPage);
        }

        pdfDocument.Pages = pagesList;
        return pdfDocument;
    }

    public (string imgFolder, string imgOutputFilename) GetPageScreenshotPath(PdfDocument pdfDocument, int pageNumber)
    {
        var imgFolder = pdfDocument.OutputFolder.Replace("//", "/");
        var imgOutputPath = $"/{Name}/Images/";

        Directory.CreateDirectory($"{imgFolder}{imgOutputPath}"); // This checks if exists, and creates the whole path too
        
        return (imgFolder, $"{imgOutputPath}page-{pageNumber}.jpg");
    }
    
    public async Task SavePageScreenshotAsync(PdfDocument pdfDocument, int pageNumber)
    {
        var (imgFolder, imgOutputFilename) = GetPageScreenshotPath(pdfDocument, pageNumber);

        using var memoryStream = pdfDocument.GetPageAsSkBitmap(pageNumber, RGBColor.White);
        await SaveAsJpegAsync(memoryStream, $"{imgFolder}{imgOutputFilename}");
    }

    public async Task<List<DocumentLine>> GetTextLinesFromPdfAsync(
        PdfDocument pdfDocument)
    {
        var dtStart = DateTime.Now;
        
        var txtCacheFolder = $"{pdfDocument.CacheFolder.Replace("//", "/")}/{Name}/Text";
        Directory.CreateDirectory(txtCacheFolder); // This checks if exists, and creates the whole path too
        
        var documentLines = new List<DocumentLine>();
        var metadataFilename = $"{txtCacheFolder}/{PositionConstants.CacheMetadataFilename}";
        
        var fromCache = pdfDocument.FromCache && File.Exists(metadataFilename);
        
        if (fromCache)
        {
            var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                metaDataFileText,
                JsonHelper.GetSerializerOptions());
            
            var pageCount = ((JsonElement)metadata!["pages"]).GetArrayLength();
            
            for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
            {
                var outputFilename = $"{txtCacheFolder}/page-{pageNumber}.json";
                List<TextBlock> pageLines = [];

                if (!File.Exists(outputFilename))
                {
                    // TODO should not happen
                    continue;
                }
                
                dtStart = DateTime.Now;
                var fileText = await File.ReadAllTextAsync(outputFilename);
                
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
                var detailCacheFilename = $"page-{page.Number}.json";
                var txtOutputFilename = $"{txtCacheFolder}/{detailCacheFilename}";
                
                pagesMetadata.Add(new Dictionary<string, object>
                {
                    { "number", page.Number },
                    { "numberOfImages", page.NumberOfImages },
                    { "text", page.Text! },
                    { "detailFilename", txtOutputFilename },
                });

                List<TextBlock> pageLines = [];

                fromCache = pdfDocument.FromCache && File.Exists(txtOutputFilename);
                
                if (fromCache)
                {
                    dtStart = DateTime.Now;
                    var fileText = await File.ReadAllTextAsync(txtOutputFilename);

                    Console.WriteLine(
                        $"Read {Name} text file page {page.Number} in {(DateTime.Now - dtStart).TotalSeconds} seconds" +
                        $"- {pdfDocument.CacheFolder}");

                    var cachedTextBlocks =
                        JsonSerializer.Deserialize<List<Models.PdfPig.DeserialisableTextBlock>>(
                            fileText,
                            JsonHelper.GetSerializerOptions())!;

                    pageLines.AddRange(cachedTextBlocks.Select(
                        cachedTextBlock => cachedTextBlock.ToPdfPigTextBlock()));

                    var pageLinesTransformed = FormatPageLines(
                        pageLines,
                        page.Number);
                    
                    documentLines.AddRange(pageLinesTransformed);
                    continue;
                }
                
                if (FormattingHelper.IsPageEmpty(page.Text))
                {
                    await File.WriteAllTextAsync(txtOutputFilename, "[]");
                    continue;
                }

                pageLines.AddRange(await GetPageLinesAsync(page.PdfPigPage!));
                
                if (pageLines.Count == 0)
                {
                    await File.WriteAllTextAsync(txtOutputFilename, "[]");
                    continue;
                }

                await File.WriteAllTextAsync(txtOutputFilename, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializerOptions()));
                
                var pageLinesTransformedX = FormatPageLines(
                    pageLines,
                    page.Number);

                documentLines.AddRange(pageLinesTransformedX);
            }
            
            var data = new Dictionary<string, object>
            {
                { "pages", pagesMetadata },
                { "allTextFilename", "pages-all.txt" }
            };
            
            await File.WriteAllTextAsync(metadataFilename, JsonSerializer.Serialize(data, JsonHelper.GetSerializerOptions()));
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
        
        return orderedPageWords
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
                var bottomRounded = lineWords.Key;
                
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
                        firstLine.BoundingBox.Bottom + blankLineGap,
                        bottomRounded + blankLineGap,
                        PositionConstants.UnknownCoordinate);

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
                    
                    if (xDiff >= 25)
                    {
                        columns.Add(new DocumentLineColumn());
                    }

                    var columnToAddTo = columns.Last();
                    columnToAddTo.Words.Add(new DocumentLineWord(
                        word!.Text,
                        null,
                        DocumentLineWordCoordinates.Convert(word.BoundingBox)
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
                    firstLine.BoundingBox.Bottom,
                    bottomRounded,
                    firstLine.BoundingBox.Left);

                resultList.Add(documentLine);
                return resultList;
            })
        .ToList();
    }
    
    private static async Task<IReadOnlyList<TextBlock>> GetPageLinesAsync(Page page)
    {
        return await Task.Run(() => RecursiveXYCut
            .Instance
            .GetBlocks(page.GetWords())
            .ToList());
    }
    
    private static async Task SaveAsJpegAsync(SKBitmap bitmap, string filePath, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        await using var stream = new FileStream(
            filePath,
            FileMode.OpenOrCreate,
            FileAccess.Write,
            FileShare.ReadWrite);
        
        data.SaveTo(stream);
        
        await stream.FlushAsync();
        stream.Close();
    }

    public void Release(PdfDocument pdfDocument)
    {
        pdfDocument.Dispose();
    }
}