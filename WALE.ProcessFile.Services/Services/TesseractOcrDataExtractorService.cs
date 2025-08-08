using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.TesseractOcr;

namespace WALE.ProcessFile.Services.Services;

public class TesseractOcrDataExtractorService(string dataPath) : IOcrDataExtractorService, IDisposable
{
    private readonly TesseractEngine _tesseractEngine = new(dataPath, "eng");

    public bool HasDirectCost => false;
    public string Name => "TesseractOcr";
    
    public Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(string imageFilename, int pageNumber, int imageNumber, PdfDocument pdfDocument)
    {
        return Task.Run(async () =>
        {
            var folder = $"{pdfDocument.CacheFolder}/TesseractOcr/Text";
            Directory.CreateDirectory(folder);
        
            var outputFilename = $"{folder}/ocr-page-{pageNumber}-image-{imageNumber}.json";
            var returnLines = new List<LineAndWords>();
            
            if (pdfDocument.FromCache && File.Exists(outputFilename))
            {
                var fileText = await File.ReadAllTextAsync(outputFilename);
                var pageLines = JsonSerializer.Deserialize<List<LineAndWords>>(
                    fileText,
                    JsonHelper.GetSerializer());
                
                returnLines.AddRange(pageLines!);
            }
            else
            {
                //  TODO - check dimensions are more then X and Y or its pointless
                
                _tesseractEngine.SetVariable("tessedit_parallelize", "1");
                using var ocrImage = Pix.LoadFromFile(imageFilename);
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
                            new(
                                coordinates.Y1,
                                coordinates.X2,
                                coordinates.Y2,
                                coordinates.X1
                            )));
                    } while (iterator.Next(PageIteratorLevel.TextLine, PageIteratorLevel.Word));

                    returnLines.Add(new LineAndWords { Text = line, Words = words });
                } while (iterator.Next(PageIteratorLevel.TextLine));
                
                await File.WriteAllTextAsync(
                    outputFilename,
                    JsonSerializer.Serialize(returnLines,
                        JsonHelper.GetSerializer()));
            }
            
            var lineNumber = 0;
            
            var results = returnLines!
                .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
                .Select(line => (FormattingHelper.Standardise(line.Text!), line.Words))
                .Select(line => new DocumentLine(
                    line.Item1,
                    lineNumber++,
                    pageNumber,
                    [new(line.Words!)],
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate))
                .ToList();

            // TODO add grouping and ordering
            
            return (IReadOnlyList<DocumentLine>)results;
        });
    }
    
    public void Dispose()
    {
        _tesseractEngine.Dispose();
        GC.SuppressFinalize(this);
    }
}