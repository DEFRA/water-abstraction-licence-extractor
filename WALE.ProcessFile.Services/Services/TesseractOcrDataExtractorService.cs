using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;

namespace WALE.ProcessFile.Services.Services;

public class TesseractOcrDataExtractorService(string dataPath) : IOcrDataExtractorService, IDisposable
{
    private readonly TesseractEngine _tesseractEngine = new(dataPath, "eng");

    public bool HasDirectCost => false;
    public string Name => "TesseractOcr";
    
    public Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(byte[] imageData, int pageNumber, int imageNumber, PdfDocument pdfDocument)
    {
        return Task.Run(async () =>
        {
            var folder = $"{pdfDocument.OutputFolder}/TesseractOcr/Text";
            Directory.CreateDirectory(folder);
        
            var outputFilename = $"{folder}/ocr-page-{pageNumber}-image-{imageNumber}.json";
            var lines = new List<LineAndWords>();
            
            if (pdfDocument.FromCache && File.Exists(outputFilename))
            {
                var fileText = await File.ReadAllTextAsync(outputFilename);
                lines = JsonSerializer.Deserialize<List<LineAndWords>>(fileText);
            }
            else
            {
                _tesseractEngine.SetVariable("tessedit_parallelize", "1");
                using var ocrImage = Pix.LoadFromMemory(imageData);
                using var page = _tesseractEngine.Process(ocrImage);
                
                using var iterator = page.GetIterator();
                iterator.Begin();

                do
                {
                    var line = iterator.GetText(PageIteratorLevel.TextLine);
                    var words = new List<DocumentLineWord?>();

                    do
                    {
                        var wordText = iterator.GetText(PageIteratorLevel.Word);
                        var wordConfidence = iterator.GetConfidence(PageIteratorLevel.Word);
                        iterator.TryGetBoundingBox(PageIteratorLevel.Word, out var coordinates);

                        words.Add(new DocumentLineWord(
                            wordText,
                            wordConfidence,
                            [
                                coordinates.X1,
                                coordinates.Y1,
                                coordinates.X2,
                                coordinates.Y2
                            ]));
                    } while (iterator.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                    lines.Add(new LineAndWords { Text = line, Words = words });
                } while (iterator.Next(PageIteratorLevel.TextLine));
                
                await File.WriteAllTextAsync(outputFilename, JsonSerializer.Serialize(lines));
            }
            
            var lineNumber = 0;
            
            var results = lines!
                .Where(line => !IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
                .Select(line => (Standardise(line.Text!), line.Words))
                .Select(line => new DocumentLine(
                    line.Item1,
                    lineNumber++,
                    pageNumber,
                    line.Words!))
                .ToList();

            return (IReadOnlyList<DocumentLine>)results;
        });
    }
    
    private class LineAndWords
    {
        public string? Text { get; set; }
        public List<DocumentLineWord?>? Words { get; set; }        
    }
    
    public void Dispose()
    {
        _tesseractEngine.Dispose();
        GC.SuppressFinalize(this);
    }
}