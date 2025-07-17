using System.Text.Json;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.DocumentLayoutAnalysis;
using UglyToad.PdfPig.DocumentLayoutAnalysis.PageSegmenter;
using UglyToad.PdfPig.Graphics.Colors;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using TextBlock = UglyToad.PdfPig.DocumentLayoutAnalysis.TextBlock;
using PdfDocument = WALE.ProcessFile.Services.Models.PdfDocument;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrDataExtractorService : INoOcrDataExtractorService
{
    public string Name => "PdfPig";
    
    public async Task<PdfDocument> GetPdfDocumentAsync(string pdfFilePath, string outputFolder, bool useCache)
    {
        var txtFolder = $"{outputFolder.Replace("//", "/")}/{Name}/Text";
        Directory.CreateDirectory(txtFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{txtFolder}/metadata.json";
        var getFromCache = useCache && File.Exists(metadataFilename);
        var pdfDocument = new PdfDocument(pdfFilePath, outputFolder, getFromCache);
        
        if (getFromCache)
        {
            // TODO load from cache
            
            var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metaDataFileText)!;

            var numberOfPages = ((JsonElement)metadata["pages"]).GetInt32();
            var pagesList = new List<PdfPage>();
            
            for (var pageNumber = 1; pageNumber <= numberOfPages; pageNumber++)
            {
                pagesList.Add(new PdfPage
                {
                    Number = pageNumber
                });
            }

            pdfDocument.Pages = pagesList;
        }

        return pdfDocument;
    }
    
    public async Task<string> SavePageScreenshotAsync(PdfDocument pdfDocument, int pageNumber)
    {
        var imgFolder = pdfDocument.OutputFolder.Replace("//", "/");
        var imgOutputPath = $"/{Name}/Images/";

        Directory.CreateDirectory($"{imgFolder}{imgOutputPath}"); // This checks if exists, and creates the whole path too
        
        var imgOutputFilename = $"/{imgOutputPath}page-{pageNumber}.png";
        
        await using var fileStream = new FileStream($"{imgFolder}{imgOutputFilename}", FileMode.Create);
        using var memoryStream = pdfDocument.GetPageAsPng(pageNumber, RGBColor.White);

        memoryStream.WriteTo(fileStream);
        return imgOutputFilename;
    }

    public async Task<List<DocumentLine>> GetTextLinesFromPdfAsync(
        PdfDocument pdfDocument)
    {
        var dtStart = DateTime.Now;
        
        var txtFolder = $"{pdfDocument.OutputFolder.Replace("//", "/")}/{Name}/Text";
        Directory.CreateDirectory(txtFolder); // This checks if exists, and creates the whole path too
        
        var documentLines = new List<DocumentLine>();
        var metadataFilename = $"{txtFolder}/metadata.json";
        
        const int roundToHorizontalLimited = 500;
        const int roundToHorizontalFull = 800;        
        
        if (pdfDocument.FromCache && File.Exists(metadataFilename))
        {
            var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(metaDataFileText);
            var pagesElement = (JsonElement)metadata!["pages"];
            var pageCount = pagesElement.GetInt32();
            
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
                
                var cachedTextBlocks = JsonSerializer.Deserialize<List<Models.PdfPig.DeserialisableTextBlock>>(fileText)!;
                
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
            
            //dtStart = DateTime.Now;
            //Console.WriteLine($"Read PdfPig text pages in {(DateTime.Now - dtStart).TotalSeconds} seconds - {pdfFilePath}");
            
            var pages = pdfDocument.Pages;
            var pageNumber = 1;
            
            foreach (var page in pdfDocument.Pages)
            {
                var txtOutputFilename = $"{txtFolder}/page-{page.Number}.json";
                List<TextBlock> pageLines = [];

                if (pdfDocument.FromCache && File.Exists(txtOutputFilename))
                {
                    dtStart = DateTime.Now;
                    var fileText = await File.ReadAllTextAsync(txtOutputFilename);

                    Console.WriteLine(
                        $"Read {Name} text file page {page.Number} in {(DateTime.Now - dtStart).TotalSeconds} seconds" +
                        $"- {pdfDocument.OutputFolder}");

                    var cachedTextBlocks =
                        JsonSerializer.Deserialize<List<Models.PdfPig.DeserialisableTextBlock>>(fileText)!;

                    pageLines.AddRange(cachedTextBlocks.Select(
                        cachedTextBlock => cachedTextBlock.ToPdfPigTextBlock()));

                    var pageLinesTransformed = FormatPageLines(
                        pageLines,
                        page.Number,
                        pageNumber > 3 ? roundToHorizontalFull : roundToHorizontalLimited);
                    
                    documentLines.AddRange(pageLinesTransformed);
                    pageNumber += 1;
                    
                    continue;
                }
                
                if (IsPageEmpty(page.PdfPigPage!.Text))
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

                await File.WriteAllTextAsync(txtOutputFilename, JsonSerializer.Serialize(pageLines));
                
                var pageLinesTransformedX = FormatPageLines(
                    pageLines,
                    page.Number,
                    pageNumber > 3 ? roundToHorizontalFull : roundToHorizontalLimited);

                documentLines.AddRange(pageLinesTransformedX);
                pageNumber += 1;
            }

            var data = new Dictionary<string, object> {{"pages", pages.Count}};
            await File.WriteAllTextAsync(metadataFilename, JsonSerializer.Serialize(data));
        }

        // Update line numbers, now in one big list
        var lineNumber = 0;
        documentLines.ForEach(documentLine => documentLine.LineNumber = lineNumber++);
        
        Console.WriteLine(
            $"Getting document text lines took {(DateTime.Now - dtStart).TotalSeconds} seconds" +
            $" - {pdfDocument.PdfFilePath}");
        
        foreach (var line in documentLines)
        {
            if (line.Text.Contains("TL545369"))
            {
                break;
            }
        }
        
        return documentLines;
    }

    public Task<IReadOnlyList<INoOcrPdfPageService>> GetPagesThatContainImagesAsync(PdfDocument pdfDocument, string pdfFilePath)
    {
        var result = pdfDocument
            .Pages
            .Where(page => IsPageEmpty(page.PdfPigPage!.Text) && page.NumberOfImages > 0)
            .Select(page => new PdfPigNoOcrPageService(page.PdfPigPage!))
            .ToList();

        return Task.FromResult((IReadOnlyList<INoOcrPdfPageService>)result);
    }
    
    private static int RoundToNearestN(double value, double roundTo)
    {
        return (int)Math.Round(value / roundTo) * (int)roundTo;
    }

    private static IReadOnlyList<DocumentLine> FormatPageLines(
        IEnumerable<TextBlock> pageLines,
        int pageNumber,
        int roundToHorizontal)
    {
        const int roundToVertical = 5;
        const int blankLineGap = 25;
        
        var lineNumber = 0;
        var previousLine = (TextLine?)null;
        
        return pageLines
            .SelectMany(textBlock => textBlock.TextLines)
            .OrderByDescending(line => RoundToNearestN(line.BoundingBox.Top, roundToVertical))
            .ThenBy(line => line.BoundingBox.Left)            
            .GroupBy(line => (
                RoundToNearestN(line.BoundingBox.Top, roundToVertical),
                RoundToNearestN(line.BoundingBox.Left, roundToHorizontal)))
            .SelectMany(lines =>
            {
                var resultList = new List<DocumentLine>();
                var verticalDistanceFromPreviousLine = previousLine?.BoundingBox.Top
                    - lines.First().BoundingBox.Top;

                var horizontalDistanceFromPreviousLine = previousLine?.BoundingBox.Left
                    - lines.First().BoundingBox.Left;                

                var containsText = false;

                foreach (var line in lines)
                {
                    if (line.Text.Contains("TL545369"))
                    {
                        containsText = true;
                        break;
                    }
                }

                if (containsText)
                {
                    
                }
                
                if (verticalDistanceFromPreviousLine >= blankLineGap)
                {
                    resultList.Add(new DocumentLine(string.Empty, lineNumber++, pageNumber, []));
                }
                
                previousLine = lines.First();

                if (lines.Count() > 1)
                {
                    
                }
                
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
                        .ToList()));
                
                return resultList;
            })
        .ToList();
    }
    
    private static async Task<IReadOnlyList<TextBlock>> GetPageLinesAsync(Page page)
    {
        const int roundTo = 5;
        
        return await Task.Run(() =>
        {
            return RecursiveXYCut
                .Instance
                .GetBlocks(page.GetWords())
                .OrderByDescending(block => RoundToNearestN(block.BoundingBox.Top, roundTo))
                .ThenBy(block => block.BoundingBox.Left)
                .ToList();
        });
    }

    public void Release(PdfDocument pdfDocument)
    {
        pdfDocument.Dispose();
    }
}