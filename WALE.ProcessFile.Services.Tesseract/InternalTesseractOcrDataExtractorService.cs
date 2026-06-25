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
        Guid fileId,
        int processRunId)
    {
        List<byte[]> bytesList;

        if (isPageScreenshot)
        {
            bytesList = await outputService.GetPageScreenshotDataAsync(
                pageNumber,
                noOcrServiceName,
                fileId);
        }
        else
        {
            var imageBytes = await cacheService.GetImageBytesAsync(
                new OcrServiceImageDataCacheRequest
                {
                    PageNumber = pageNumber,
                    ImageNumber = imageNumber,
                    FileId = fileId,
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
                if (bytes.Length == 0)
                {
                    ConsoleHelper.WriteLine($"WARNING - TesseractInternal - Couldn't process as bytes length was zero - {fileId}");
                    continue;
                }
                
                var returnList = await GetDataFromTesseractAsync(bytes);
                var numberOfWords = returnList.Sum(line => line.Words?.Count ?? 0);

                if (numberOfWords <= maxNumberOfWords)
                {
                    continue;
                }

                maxNumberOfWords = numberOfWords;
                textLines = returnList;

                canSave = true;
            }
            catch (TimeoutException tex)
            {
                ConsoleHelper.WriteLine($"ERROR - TesseractInternal - Timeout {tex}");
                canSave = true;
            }
            catch (Exception e)
            {
                ConsoleHelper.WriteLine($"ERROR - TesseractInternal - {e} - {fileId}");
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
            FileId = fileId,
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
    
    private async Task<List<LineAndWords>> GetDataFromTesseractAsync(byte[] bytes)
    {
        var ocrImage = Pix.LoadFromMemory(bytes);
        
        const int minHeight = 200;
        const int minWidth = 200;

        if (minHeight > ocrImage.Height || minWidth > ocrImage.Width)
        {
            return [];
        }
        
        const int maxExecutionTimeMs = 30_000;
        var timeout = TimeSpan.FromMilliseconds(maxExecutionTimeMs);
        
        var cts = new CancellationTokenSource();
        cts.CancelAfter(timeout);
        
        var token = cts.Token;
        
        var processTask = Task.Run(() =>
        {
            try
            {
                var tesseractEngine = GetEngine();
                token.ThrowIfCancellationRequested();
                
                var page = tesseractEngine.Process(ocrImage, ConvertPageSegMode(pageSegMode));
            
                token.ThrowIfCancellationRequested();
                
                var textLines = GetTextLinesFromPageAsync(page, token);
                return textLines;
            }
            catch (Exception e)
            {
                ConsoleHelper.WriteLine($"ERROR - TesseractInternal - (Inside task run) {e}");
                return null;
            }
        }, token);
        
        const int maxExecutionTimeMs2 = 31_000;
        var timeout2 = TimeSpan.FromMilliseconds(maxExecutionTimeMs2);
        
        var delay = Task.Delay(timeout2);
        var raceResult = await Task.WhenAny(processTask, delay);

        if (raceResult == delay)
        {
            ConsoleHelper.WriteLine("ERROR - TesseractInternal - Timeout occured (race check found)");
            throw new TimeoutException();
        }

        var result = await processTask;
        
        if (raceResult == null)
        {
            ConsoleHelper.WriteLine("ERROR - TesseractInternal - Timeout occured (cancellation token respected)");
            throw new TimeoutException();
        }
        
        return result!;
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
    
    private static List<LineAndWords> GetTextLinesFromPageAsync(Page page, CancellationToken token)
    {
        using var iterator = page.GetIterator();
        iterator.Begin();

        return ToPageLines(iterator, token);
    }

    private static List<LineAndWords> ToPageLines(ResultIterator? iterator, CancellationToken token)
    {
        var returnLines = new List<LineAndWords>();
        
        do
        {
            token.ThrowIfCancellationRequested();
            
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
                token.ThrowIfCancellationRequested();
                
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
        engine.SetVariable("tessedit_parallelize", "10");
        engine.SetVariable("user_defined_dpi", "200");

        return engine;
    }
}