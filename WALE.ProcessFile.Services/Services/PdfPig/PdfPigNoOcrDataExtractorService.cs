using System.Text.Json;
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
using static WALE.ProcessFile.Services.Helpers.DataHelpers;

namespace WALE.ProcessFile.Services.Services.PdfPig;

public class PdfPigNoOcrDataExtractorService : INoOcrDataExtractorService
{
    public string Name => "PdfPig";
    private const int RoundToVertical = 11;
    
    public async Task<PdfDocument> GetPdfDocumentAsync(string pdfFilePath, string outputFolder, bool useCache)
    {
        var txtFolder = $"{outputFolder.Replace("//", "/")}/{Name}/Text";
        Directory.CreateDirectory(txtFolder); // This checks if exists, and creates the whole path too

        var metadataFilename = $"{txtFolder}/{PositionConstants.CacheMetadataFilename}";
        var getFromCache = useCache && File.Exists(metadataFilename);
        var pdfDocument = new PdfDocument(pdfFilePath, outputFolder, getFromCache);
        
        if (getFromCache)
        {
            // TODO load from cache
            
            var metaDataFileText = await File.ReadAllTextAsync(metadataFilename);
            var metadata = JsonSerializer.Deserialize<Dictionary<string, object>>(
                metaDataFileText,
                SharedHelper.GetSerializer())!;

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
    
    public async Task<PdfPage> SavePageScreenshotAsync(PdfDocument pdfDocument, int pageNumber)
    {
        var imgFolder = pdfDocument.OutputFolder.Replace("//", "/");
        var imgOutputPath = $"/{Name}/Images/";

        Directory.CreateDirectory($"{imgFolder}{imgOutputPath}"); // This checks if exists, and creates the whole path too
        
        var imgOutputFilename = $"/{imgOutputPath}page-{pageNumber}.png";
        
        await using var fileStream = new FileStream($"{imgFolder}{imgOutputFilename}", FileMode.Create);
        using var memoryStream = pdfDocument.GetPageAsPng(pageNumber, RGBColor.White);

        memoryStream.WriteTo(fileStream);
        return new PdfPage
        {
            Number = pageNumber,
            NumberOfImages = pdfDocument.Pages[pageNumber-1].NumberOfImages
        };
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
                SharedHelper.GetSerializer());
            
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
                    SharedHelper.GetSerializer())!;
                
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
                            SharedHelper.GetSerializer())!;

                    pageLines.AddRange(cachedTextBlocks.Select(
                        cachedTextBlock => cachedTextBlock.ToPdfPigTextBlock()));

                    var pageLinesTransformed = FormatPageLines(
                        pageLines,
                        page.Number,
                        page.Number > 3 ? roundToHorizontalFull : roundToHorizontalLimited);
                    
                    documentLines.AddRange(pageLinesTransformed);
                    continue;
                }
                
                if (IsPageEmpty(page.Text))
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

                await File.WriteAllTextAsync(txtOutputFilename, JsonSerializer.Serialize(pageLines, SharedHelper.GetSerializer()));
                
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
            
            await File.WriteAllTextAsync(metadataFilename, JsonSerializer.Serialize(data, SharedHelper.GetSerializer()));
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
        if (pdfDocument.Pages.Any(p => p.PdfPigPage == null))
        {
            
        }
        
        var result = pdfDocument
            .Pages
            .Where(page => IsPageEmpty(page.Text) && page.NumberOfImages > 0)
            .Select(page => new PdfPigNoOcrPageService(page.PdfPigPage!))
            .ToList();

        return Task.FromResult((IReadOnlyList<INoOcrPdfPageService>)result);
    }
    
    private static int RoundToNearestN(double value, double roundTo)
    {
        return (int)Math.Round(value / roundTo) * (int)roundTo;
    }

    private static double MidPoint(double? pos1, double? pos2)
    {
        if (pos1 == null || pos2 == null)
        {
            return 0;
        }
        
        var distance = pos2.Value - pos1.Value;
        return pos1.Value + (distance / 2);
    }
    
    private static IReadOnlyList<DocumentLine> FormatPageLines(
        IEnumerable<TextBlock> pageLines,
        int pageNumber,
        int roundToHorizontal)
    {
        const int blankLineGap = 25;
        
        var lineNumber = 0;
        var previousLine = (TextLine?)null;
        
        return pageLines
            .SelectMany(textBlock => textBlock.TextLines)
            .OrderByDescending(line => RoundToNearestN(
                MidPoint(line.BoundingBox.Top, line.BoundingBox.Bottom),
                RoundToVertical))
            .ThenBy(line => MidPoint(line.BoundingBox.Left, line.BoundingBox.Right))
            .GroupBy(line => (
                RoundToNearestN(
                    MidPoint(line.BoundingBox.Top, line.BoundingBox.Bottom),
                    RoundToVertical),
                RoundToNearestN(
                    MidPoint(line.BoundingBox.Left, line.BoundingBox.Right),
                    roundToHorizontal)))
            .SelectMany(lines =>
            {
                if (lines.Any(x => x.Text.Contains("From 01 April")))
                {
                    
                }
                
                var resultList = new List<DocumentLine>();
                var firstLine = lines.First();
                
                var verticalDistanceFromPreviousLine=
                    MidPoint(previousLine?.BoundingBox.Top, previousLine?.BoundingBox.Bottom)
                    - MidPoint(firstLine.BoundingBox.Top, firstLine.BoundingBox.Bottom);

                /*var horizontalDistanceFromPreviousLine = 
                    previousLine?.BoundingBox.Left
                    - lines.First().BoundingBox.Left; */               

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
        return await Task.Run(() =>
        {
            return RecursiveXYCut
                .Instance
                .GetBlocks(page.GetWords())
                .OrderByDescending(block => RoundToNearestN(
                    MidPoint(block.BoundingBox.Top, block.BoundingBox.Bottom),
                    RoundToVertical))
                .ThenBy(block => MidPoint(block.BoundingBox.Left, block.BoundingBox.Right))
                .ToList();
        });
    }

    public void Release(PdfDocument pdfDocument)
    {
        pdfDocument.Dispose();
    }
}