using Tesseract;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.Tesseract;

public class InternalTesseractOcrDataExtractorService(
    IOutputService outputService,
    ICacheService cacheService,
    string dataPath,
    Core.Enums.PageSegMode pageSegMode)
{
    public async Task<List<LineAndWords>> ProcessAsync(
        string noOcrServiceName,
        int pageNumber,
        int imageNumber,
        bool isPageScreenshot,
        string imageReference,
        string pdfFilename,
        int processRunId)
    {
        List<byte[]> bytesList;

        if (isPageScreenshot)
        {
            bytesList = await outputService.GetPageScreenshotDataAsync(
                pageNumber,
                noOcrServiceName,
                pdfFilename);
        }
        else
        {
            var imageBytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
            {
                PageNumber = pageNumber,
                ImageNumber = imageNumber,
                Filename = pdfFilename,
                NoOcrServiceName = noOcrServiceName,
                Extension = FileHelper.GetImageExtension(imageReference)
            });
        
            bytesList =
            [
                imageBytes!
            ];
        }

        if (bytesList.Count == 0)
        {
            throw new Exception("Image was not found");
        }

        var textLines = new List<LineAndWords>();
        var maxNumberOfWords = -1;
        var canSave = false;
                
        foreach (var bytes in bytesList)
        {
            try
            {
                var returnList = GetDataFromTesseract(bytes);
                var numberOfWords = returnList.Sum(line => line.Words!.Count);

                if (numberOfWords <= maxNumberOfWords)
                {
                    continue;
                }
                        
                maxNumberOfWords = numberOfWords;
                textLines = returnList;

                canSave = true;
            }
            catch (Exception e)
            {
                ConsoleHelper.WriteLine($"ERROR - TesseractInternal - {e}");
                // TODO log
            }
        }
    
        if (!canSave)
        {
            return textLines;
        }
        
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filename = pdfFilename,
            OcrServiceName = $"TesseractOcr-{pageSegMode}",
            ProcessRunId = processRunId
        };

        if (isPageScreenshot)
        {
            await cacheService.SaveTemporaryOcrScreenshotTextAsync(request, textLines);        
        }
        else
        {
            await cacheService.SaveTemporaryOcrImageTextAsync(request, textLines);        
        }

        return textLines;
    }
    
    private List<LineAndWords> GetDataFromTesseract(byte[] bytes)
    {
        var ocrImage = Pix.LoadFromMemory(bytes);
        
        const int minHeight = 200;
        const int minWidth = 200;

        if (minHeight > ocrImage.Height || minWidth > ocrImage.Width)
        {
            return [];
        }
        
        Page? page = null;
        
        var tesseractEngine = GetEngine();
        var engine = tesseractEngine;
        
        var processTask = Task.Run(() =>
        {
            //var dtProcessStart = DateTime.Now;
            page = engine.Process(ocrImage, ConvertPageSegMode(pageSegMode));
            //var tsProcess = (DateTime.Now - dtProcessStart).TotalMilliseconds;
            
            //var dtIterateStart = DateTime.Now;
            var textLines = GetTextLinesFromPageAsync(page);
            //var tsIterate = (DateTime.Now - dtIterateStart).TotalMilliseconds;
            
            return textLines;
        });
            
        const int maxExecutionTimeMs = 30_000;
        var isCompletedSuccessfully = processTask.Wait(TimeSpan.FromMilliseconds(maxExecutionTimeMs));

        page?.Dispose();
        tesseractEngine.Dispose();

        if (!isCompletedSuccessfully)
        {
            throw new TimeoutException($"Tesseract process timed out after {maxExecutionTimeMs} seconds");
        }
        
        return processTask.Result;
    }

    private static PageSegMode ConvertPageSegMode(Core.Enums.PageSegMode pageSegMode)
    {
        return pageSegMode switch
        {
            Core.Enums.PageSegMode.SparseTextOsd => PageSegMode.SparseTextOsd,
            Core.Enums.PageSegMode.Auto => PageSegMode.Auto,
            _ => throw new ArgumentOutOfRangeException(nameof(pageSegMode), pageSegMode, null)
        };
    }
    
    private static List<LineAndWords> GetTextLinesFromPageAsync(Page page)
    {
        using var iterator = page.GetIterator();
        iterator.Begin();

        return ToPageLines(iterator);
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

            returnLines.Add(
                new LineAndWords
                {
                    Words = words
                });
        } while (iterator.Next(PageIteratorLevel.TextLine));
        
        return returnLines;
    }
    
    private TesseractEngine GetEngine()
    {
        var engine = new TesseractEngine(dataPath, "eng");
        engine.SetVariable("tessedit_parallelize", "1");
        engine.SetVariable("user_defined_dpi", "200");

        return engine;
    }
}