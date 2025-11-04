using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

namespace WALE.ProcessFile.Services.Services;

public class TesseractOcrDataExtractorService(
    string dataPath,
    PageSegMode pageSegMode,
    ICacheService cacheService)
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
            int processRunId)
    {
        var returnLines = new List<LineAndWords>();
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilepath,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        var cacheFileText = await cacheService.GetOcrImageTextAsync(request);
        
        if (pdfDocument.FromCache && !string.IsNullOrEmpty(cacheFileText))
        {
            var pageLines = JsonSerializer.Deserialize<List<LineAndWords>>(
                cacheFileText,
                JsonHelper.GetSerializerOptions());
            
            returnLines.AddRange(pageLines!);
        }
        else
        {
            Pix? ocrImage;

            try
            {
                var bytes = await cacheService.GetImageBytesAsync(
                    new OcrServiceImageDataCacheRequest
                    {
                        PageNumber = pageNumber,
                        ImageNumber = imageNumber,
                        Filepath = pdfFilepath,
                        NoOcrServiceName = PdfDataExtractorService.Name,
                        Extension = imageReference.Split('.').Last()
                    });

                if (bytes == null)
                {
                    throw new Exception("Image was not found");
                }
                
                ocrImage = Pix.LoadFromMemory(bytes);
            }
            catch
            {
                if (!imageReference.Contains(".jpg", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw;
                }

                var bytes = await cacheService.SaveDeflatedImageAsync(
                    request.Filepath,
                    request.ImageNumber,
                    request.PageNumber);
                
                ocrImage = Pix.LoadFromMemory(bytes);
            }

            try
            {
                returnLines = await Task.Run(() => GetDataFromTesseract(ocrImage));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            await cacheService.SaveOcrImageTextAsync(request, returnLines);
        }
        
        const int lineHeight = 15;
        return OcrHelper.Group(returnLines, pageNumber, lineHeight, 1);
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
                    )));
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