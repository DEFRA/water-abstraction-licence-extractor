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
    IOutputService outputService)
    : IOcrDataExtractorService, IDisposable
{
    public bool HasDirectCost => false;
    public string Name => $"TesseractOcr-{pageSegMode}";

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
        var isPageScreenshot = imageReference.StartsWith("Screenshot");
        
        var returnLines = new List<LineAndWords>();
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilepath,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        var cacheFileText = isPageScreenshot
            ? await cacheService.GetOcrScreenshotTextAsync(request)
            : await cacheService.GetOcrImageTextAsync(request);
        
        if (pdfDocument.FromCache && !string.IsNullOrEmpty(cacheFileText))
        {
            var imageLines = JsonSerializer.Deserialize<List<LineAndWords>>(
                cacheFileText,
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
            
            var ocrImage = Pix.LoadFromMemory(bytes);

            try
            {
                returnLines = await Task.Run(() => GetDataFromTesseract(ocrImage));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (isPageScreenshot)
            {
                await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);                
            }
            else
            {
                await cacheService.SaveOcrImageTextAsync(request, returnLines);                
            }
        }
        
        const int lineHeight = 21;
        const int wordGap = 200;
        const int minWordHeight = 15;
        const int maxPercentHeightDiff = 0;
        const int maxDiffBetweenWordTop = 100;
        
        return OcrHelper.Group(
            returnLines,
            false,
            pageNumber,
            lineHeight,
            wordGap,
            minWordHeight,
            maxPercentHeightDiff,
            maxDiffBetweenWordTop);
    }

    private List<LineAndWords> GetDataFromTesseract(Pix ocrImage)
    {
        try
        {
            //  TODO - check dimensions are more then X and Y or its pointless
            TesseractEngine tesseractEngine = new(dataPath, "eng");
            tesseractEngine.SetVariable("tessedit_parallelize", "1");
                    
            var page = tesseractEngine.Process(ocrImage, pageSegMode);

            // ReSharper disable once AccessToDisposedClosure
            var task = Task.Run(() => GetTextLinesFromPageAsync(page));
                
            const int maxExecutionTimeMs = 30_000;
            var isCompletedSuccessfully = task.Wait(TimeSpan.FromMilliseconds(maxExecutionTimeMs));

            return !isCompletedSuccessfully ? [] : task.Result;
        }
        catch (Exception e)
        {
            //Console.SetOut(TextWriter.Null);
            
            Console.WriteLine(e);
            return [];
        }
    }

    private List<LineAndWords> GetTextLinesFromPageAsync(Page page)
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
            return [];
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

            var line = new string(lineText
                .Where(ch => ch != '\n')
                .ToArray());

            var words = new List<DocumentLineWord?>();

            do
            {
                var wordText = iterator.GetText(PageIteratorLevel.Word);
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
        GC.SuppressFinalize(this);
    }
}