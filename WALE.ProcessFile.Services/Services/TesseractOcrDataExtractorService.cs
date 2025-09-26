using System.Text.Json;
using Tesseract;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Models.TesseractOcr;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Services;

public class TesseractOcrDataExtractorService(string dataPath) : IOcrDataExtractorService, IDisposable
{
    private readonly TesseractEngine _tesseractEngine = new(dataPath, "eng");

    public bool HasDirectCost => false;
    public string Name => "TesseractOcr";
    
    public Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(string imageFilepath, int pageNumber, int imageNumber, PdfDocument pdfDocument)
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
                    JsonHelper.GetSerializerOptions());
                
                returnLines.AddRange(pageLines!);
            }
            else
            {
                //  TODO - check dimensions are more then X and Y or its pointless
                
                _tesseractEngine.SetVariable("tessedit_parallelize", "1");

                Pix? ocrImage;

                try
                {
                    ocrImage = Pix.LoadFromFile(imageFilepath);
                }
                catch
                {
                    if (!imageFilepath.Contains(".jpg", StringComparison.InvariantCultureIgnoreCase))
                    {
                        throw;
                    }
                    
                    var bytAry = await File.ReadAllBytesAsync(imageFilepath);
                    var deflated = PdfPigNoOcrImageService.Deflate(bytAry);

                    var imageFilenameDeflated = imageFilepath.Replace(".jpg", "-deflated.jpg",
                        StringComparison.InvariantCultureIgnoreCase);
                    await File.WriteAllBytesAsync(imageFilenameDeflated, deflated);

                    ocrImage = Pix.LoadFromFile(imageFilenameDeflated);
                }
                
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
                        JsonHelper.GetSerializerOptions()));
            }
            
            var lineNumber = 0;
            
            // TODO probably need to standardise the line
            
            var results = returnLines!
                .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
                .Select(line => (line.Text!, line.Words))
                .Select(line =>
                {
                    var documentLine = new DocumentLine(
                        lineNumber++,
                        pageNumber,
                        [new(line.Item1, line.Words!)],
                        PositionConstants.UnknownCoordinate,
                        PositionConstants.UnknownCoordinate,
                        PositionConstants.UnknownCoordinate);
                    
                    return documentLine;
                })
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