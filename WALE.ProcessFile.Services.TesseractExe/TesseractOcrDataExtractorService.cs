using Tesseract;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.TesseractExe;

public class TesseractOcrDataExtractorService(
    string dataPath,
    PageSegMode pageSegMode)
{
    public List<LineAndWords> GetDataFromTesseract(byte[] bytes)
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
            page = engine.Process(ocrImage, pageSegMode);
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
        
        return !isCompletedSuccessfully ? [] : processTask.Result;
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

            returnLines.Add(new LineAndWords { Text = line, Words = words });
        } while (iterator.Next(PageIteratorLevel.TextLine));
        
        return returnLines;
    }
    
    private TesseractEngine GetEngine()
    {
        var engine = new TesseractEngine(dataPath, "eng");
        engine.SetVariable("tessedit_parallelize", "1");

        return engine;
    }
}