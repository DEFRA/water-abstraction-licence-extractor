using System.Text.Json;
using SkiaSharp;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
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
    private const int LineHeight = 11;
    
    public async Task<PdfDocument> GetPdfDocumentAsync(string pdfFilePath, string outputFolder)
    {
        var txtFolder = $"{outputFolder.Replace("//", "/")}/{Name}/Text";
        Directory.CreateDirectory(txtFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{txtFolder}/{PositionConstants.CacheMetadataFilename}";
        var getFromCache = File.Exists(metadataFilename);
        var pdfDocument = new PdfDocument(pdfFilePath, outputFolder, getFromCache);
        
        if (getFromCache)
        {
            var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                metaDataFileText,
                JsonHelper.GetSerializer())!;

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
        }

        return pdfDocument;
    }
    
    public Task<PdfPage> SavePageScreenshotAsync(PdfDocument pdfDocument, int pageNumber)
    {
        var imgFolder = pdfDocument.OutputFolder.Replace("//", "/");
        var imgOutputPath = $"/{Name}/Images/";

        Directory.CreateDirectory($"{imgFolder}{imgOutputPath}"); // This checks if exists, and creates the whole path too
        
        var imgOutputFilename = $"/{imgOutputPath}page-{pageNumber}.jpg";

        using var memoryStream = pdfDocument.GetPageAsSkBitmap(pageNumber, RGBColor.White);
        SaveAsJpeg(memoryStream, $"{imgFolder}{imgOutputFilename}");
        
        var page = pdfDocument.Pages[pageNumber - 1];
        
        return Task.FromResult(new PdfPage
        {
            Number = pageNumber,
            NumberOfImages = page.NumberOfImages
        });
    }

    public async Task<List<DocumentLine>> GetTextLinesFromPdfAsync(
        PdfDocument pdfDocument)
    {
        var dtStart = DateTime.Now;
        
        var txtFolder = $"{pdfDocument.OutputFolder.Replace("//", "/")}/{Name}/Text";
        Directory.CreateDirectory(txtFolder); // This checks if exists, and creates the whole path too
        
        var documentLines = new List<DocumentLine>();
        var metadataFilename = $"{txtFolder}/{PositionConstants.CacheMetadataFilename}";
        
        const int roundToHorizontalLimited = 500;
        const int roundToHorizontalFull = 900;        
        
        if (pdfDocument.FromCache && File.Exists(metadataFilename))
        {
            var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                metaDataFileText,
                JsonHelper.GetSerializer());
            
            var pageCount = ((JsonElement)metadata!["pages"]).GetArrayLength();
            
            for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
            {
                var outputFilename = $"{txtFolder}/page-{pageNumber}.json";
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
                    JsonHelper.GetSerializer())!;
                
                pageLines.AddRange(cachedTextBlocks.Select(
                    cachedTextBlock => cachedTextBlock.ToPdfPigTextBlock()));
                
                var pageLinesTransformed = FormatPageLines(
                    pageLines,
                    pageNumber,
                    pageNumber > 3 ? roundToHorizontalFull : roundToHorizontalLimited);

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
                var txtOutputFilename = $"{txtFolder}/{detailCacheFilename}";
                
                pagesMetadata.Add(new Dictionary<string, object>
                {
                    { "number", page.Number },
                    { "numberOfImages", page.NumberOfImages },
                    { "text", page.Text! },
                    { "detailFilename", txtOutputFilename },
                });

                List<TextBlock> pageLines = [];                
                
                if (pdfDocument.FromCache && File.Exists(txtOutputFilename))
                {
                    dtStart = DateTime.Now;
                    var fileText = await File.ReadAllTextAsync(txtOutputFilename);

                    Console.WriteLine(
                        $"Read {Name} text file page {page.Number} in {(DateTime.Now - dtStart).TotalSeconds} seconds" +
                        $"- {pdfDocument.OutputFolder}");

                    var cachedTextBlocks =
                        JsonSerializer.Deserialize<List<Models.PdfPig.DeserialisableTextBlock>>(
                            fileText,
                            JsonHelper.GetSerializer())!;

                    pageLines.AddRange(cachedTextBlocks.Select(
                        cachedTextBlock => cachedTextBlock.ToPdfPigTextBlock()));

                    var pageLinesTransformed = FormatPageLines(
                        pageLines,
                        page.Number,
                        page.Number > 3 ? roundToHorizontalFull : roundToHorizontalLimited);
                    
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

                await File.WriteAllTextAsync(txtOutputFilename, JsonSerializer.Serialize(pageLines, JsonHelper.GetSerializer()));
                
                var pageLinesTransformedX = FormatPageLines(
                    pageLines,
                    page.Number,
                    page.Number > 3 ? roundToHorizontalFull : roundToHorizontalLimited);

                documentLines.AddRange(pageLinesTransformedX);
            }
            
            var data = new Dictionary<string, object>
            {
                { "pages", pagesMetadata },
                { "allTextFilename", "pages-all.txt" }
            };
            
            await File.WriteAllTextAsync(metadataFilename, JsonSerializer.Serialize(data, JsonHelper.GetSerializer()));
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
        IReadOnlyList<TextBlock> pageLines,
        int pageNumber,
        int roundToHorizontal)
    {
        if (pageLines.Count == 0)
        {
            return [];
        }
        
        const int blankLineGap = 25;
        
        var lineNumber = 0;
        var previousLine = (TextLine?)null;

        var orderedPageLines = pageLines
            .SelectMany(textBlock => textBlock.TextLines)
            .OrderByDescending(line => LineSnappingHelper.RoundToNearestN(
                line.BoundingBox.Centroid.Y,
                LineHeight))
            .ThenBy(line => line.BoundingBox.Centroid.X)
            .ToList();

        var marginTop = orderedPageLines[0].BoundingBox.Top;
        const double normalFontSizeMax = 8.5;
        
        foreach (var pageLine in orderedPageLines)
        {
            if (!(pageLine.BoundingBox.Height <= normalFontSizeMax))
            {
                continue;
            }
            
            marginTop = pageLine.BoundingBox.Top;
            break;
        }
        
        return orderedPageLines
            .GroupBy(line => (
                LineSnappingHelper.SnapToPageRow(
                    line.BoundingBox.Centroid.Y,
                    LineHeight,
                    marginTop),
                LineSnappingHelper.RoundToNearestN(
                    line.BoundingBox.Centroid.X,
                    roundToHorizontal)))
            .SelectMany(lines =>
            {
                var resultList = new List<DocumentLine>();
                var firstLine = lines.First();

                var verticalDistanceFromPreviousLine =
                    previousLine?.BoundingBox.Centroid.Y
                    - firstLine.BoundingBox.Centroid.Y;         

                if (verticalDistanceFromPreviousLine >= blankLineGap)
                {
                    resultList.Add(
                        new DocumentLine(
                            string.Empty,
                            lineNumber++,
                            pageNumber,
                            [],
                            PositionConstants.UnknownCoOrdinate,
                            PositionConstants.UnknownCoOrdinate,
                            PositionConstants.UnknownCoOrdinate,
                            PositionConstants.UnknownCoOrdinate));
                }
                
                previousLine = lines.First();
                
                var text = string.Join(' ', lines);
                var words = lines.SelectMany(line => line.Words);

                resultList.Add(new DocumentLine(
                    text,
                    lineNumber++,
                    pageNumber,
                    words.Select(word => new DocumentLineWord(
                            word.Text,
                            null,
                            [
                                word.BoundingBox.Top,
                                word.BoundingBox.Left,
                                word.BoundingBox.Bottom,
                                word.BoundingBox.Right
                            ]))
                        .ToList(),
                    firstLine.BoundingBox.Centroid.Y,
                    lines.Key.Item1,
                    firstLine.BoundingBox.Centroid.X,
                    lines.Key.Item2));
                
                return resultList;
            })
        .ToList();
    }
    
    private static async Task<IReadOnlyList<TextBlock>> GetPageLinesAsync(Page page)
    {
        return await Task.Run(() =>
        {
            return RecursiveXYCut
                .Instance
                .GetBlocks(page.GetWords())
                .OrderByDescending(block => LineSnappingHelper.RoundToNearestN(
                    block.BoundingBox.Centroid.Y,
                    LineHeight))
                .ThenBy(block => block.BoundingBox.Centroid.X)
                .ToList();
        });
    }
    
    private static void SaveAsJpeg(SKBitmap bitmap, string filePath, int quality = 60)
    {
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Jpeg, quality);
        using var stream = File.OpenWrite(filePath);
        
        data.SaveTo(stream);
    }

    public void Release(PdfDocument pdfDocument)
    {
        pdfDocument.Dispose();
    }
}