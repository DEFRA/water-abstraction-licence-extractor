using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Services;

public class TesseractOcrDataExtractorService(
    string dataPath,
    PageSegMode pageSegMode,
    ICacheService cacheService,
    IOutputService outputService,
    int id = -1)
    : IOcrDataExtractorService, IDisposable
{
    public bool HasDirectCost => false;
    public string Name => $"TesseractOcr-{pageSegMode}";
    public int Id { get; set; } = id;
    
    private TesseractEngine? _engine;

    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(
            string imageReference,
            string pdfFilepath,
            int pageNumber,
            int imageNumber,
            PdfDocument pdfDocument,
            int processRunId,
            string noOcrServiceName)
    {
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilepath,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        var isPageScreenshot = imageReference.StartsWith("Screenshot");
        var returnLines = new List<LineAndWords>();
        
        var cachedJson = isPageScreenshot
            ? await cacheService.GetOcrScreenshotTextAsync(request)
            : await cacheService.GetOcrImageTextAsync(request);
        
        if (pdfDocument.FromCache && !string.IsNullOrEmpty(cachedJson))
        {
            var imageLines = JsonSerializer.Deserialize<List<LineAndWords>>(
                cachedJson,
                JsonHelper.GetSerializerOptions());
            
            returnLines.AddRange(imageLines!);
        }
        else
        {
            byte[]? bytes;

            if (isPageScreenshot)
            {
                bytes = await outputService.GetPageScreenshotDataAsync(
                    pageNumber,
                    PdfDataExtractorService.Name,
                    pdfFilepath);
            }
            else
            {
                bytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
                {
                    PageNumber = pageNumber,
                    ImageNumber = imageNumber,
                    Filepath = pdfFilepath,
                    NoOcrServiceName = PdfDataExtractorService.Name,
                    Extension = FileHelper.GetImageExtension(imageReference)
                });
            }

            if (bytes == null)
            {
                throw new Exception("Image was not found");
            }
            
            returnLines = GetDataFromTesseract(bytes);
            
            if (isPageScreenshot)
            {
                await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);                
            }
            else
            {
                await cacheService.SaveOcrImageTextAsync(request, returnLines);                
            }
        }
        
        const int horizontalColumnGap = 200;
        const int minFontSize = 15;
        const int maxPercentHeightDiff = 0;

        const int lineHeight = 21;
        const int maxNegativeDiffBetweenWordTop = -100;
        const int maxPositiveDiffBetweenWordTop = 100;
        const int considerableOverlapAmount = 3; // TODO check and tweak

        return OcrHelper.Group(
            returnLines,
            false,
            pageNumber,
            horizontalColumnGap,
            minFontSize,
            considerableOverlapAmount,
            lineHeight,
            maxPercentHeightDiff,
            maxNegativeDiffBetweenWordTop,
            maxPositiveDiffBetweenWordTop);
    }
    
    private TesseractEngine GetEngine()
    {
        if (_engine != null)
        {
            return _engine;
        }
        
        _engine = new TesseractEngine(dataPath, "eng");
        _engine.SetVariable("tessedit_parallelize", "0");

        return _engine;
    }
    
    private List<LineAndWords> GetDataFromTesseract(byte[] bytes)
    {
        try
        {
            var ocrImage = Pix.LoadFromMemory(bytes);
            
            const int minHeight = 200;
            const int minWidth = 200;

            if (minHeight > ocrImage.Height || minWidth > ocrImage.Width)
            {
                return [];
            }
            
            var tesseractEngine = GetEngine();

            Page? page = null;
            
            // ReSharper disable once AccessToDisposedClosure
            var task = Task.Run(() =>
            {
                //var dtProcessStart = DateTime.Now;
                page = tesseractEngine.Process(ocrImage, pageSegMode);
                //var tsProcess = (DateTime.Now - dtProcessStart).TotalMilliseconds;
                
                //var dtIterateStart = DateTime.Now;
                var textLines = GetTextLinesFromPageAsync(page);
                //var tsIterate = (DateTime.Now - dtIterateStart).TotalMilliseconds;
                
                return textLines;
            });
                
            const int maxExecutionTimeMs = 30_000;
            var isCompletedSuccessfully = task.Wait(TimeSpan.FromMilliseconds(maxExecutionTimeMs));

            page?.Dispose();
            
            if (!isCompletedSuccessfully)
            {
                tesseractEngine.Dispose();
                _engine = null;
            }
            
            return !isCompletedSuccessfully ? [] : task.Result;
        }
        catch (Exception e)
        {
            throw;
            //Console.SetOut(TextWriter.Null);
            
            Console.WriteLine(e);
            return [];
        }
    }

    private static List<LineAndWords> GetTextLinesFromPageAsync(Page page)
    {
        try
        {
            using var iterator = page.GetIterator();
            iterator.Begin();

            return ToPageLines(iterator);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
            //return [];
        }
    }

    private static List<LineAndWords> ToPageLines(ResultIterator? iterator)
    {
        var returnLines = new List<LineAndWords>();
        
        do
        {
            var lineText = iterator!.GetText(PageIteratorLevel.TextLine);

            if (lineText == null)
            {
                continue;
            }

            //var dtLineStart = DateTime.Now;
            var line = new string(lineText
                .Where(ch => ch != '\n')
                .ToArray());
            //var tsLine = (DateTime.Now - dtLineStart).TotalMilliseconds;

            var words = new List<DocumentLineWord?>();

            do
            {
                //var dtWordStart = DateTime.Now;
                var wordText = iterator.GetText(PageIteratorLevel.Word);
                //var tsWord = (DateTime.Now - dtWordStart).TotalMilliseconds;
                
                var wordConfidence = iterator.GetConfidence(PageIteratorLevel.Word);
                iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var coordinates);

                words.Add(new DocumentLineWord(
                    wordText,
                    wordConfidence,
                    new DocumentLineWordCoordinates(
                        coordinates.Y1,
                        coordinates.X2,
                        coordinates.Y2,
                        coordinates.X1
                    ),
                    null));
            } while (iterator.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

            returnLines.Add(new LineAndWords { Text = line, Words = words });
        } while (iterator.Next(PageIteratorLevel.TextLine));
        
        return returnLines;
    }
    
    public void Dispose()
    {
        _engine?.Dispose();
        GC.SuppressFinalize(this);
    }
}