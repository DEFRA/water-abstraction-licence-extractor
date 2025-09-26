using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using Tesseract;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Services;

public class AzureAiVisionOcrDataExtractorService(string endpoint, string key) : IOcrDataExtractorService
{
    public bool HasDirectCost => true;
    public string Name => "AzureAiVisionOcr";

    private readonly ComputerVisionClient _client = Authenticate(endpoint, key);

    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(string imageFilepath, int pageNumber, int imageNumber, PdfDocument pdfDocument)
    {
        var returnLines = new List<(string Text, IList<Word> Words)>();

        var folder = $"{pdfDocument.CacheFolder}/{Name}/Text";
        var outputFilename = $"{folder}/ocr-page-{pageNumber}-image-{imageNumber}.json";
        
        if (pdfDocument.FromCache && File.Exists(outputFilename))
        {
            var cachedText = await File.ReadAllTextAsync(outputFilename);
            var cachedPage = JsonSerializer.Deserialize<ReadResult>(
                cachedText,
                JsonHelper.GetSerializerOptions());

            var pageLines = ToPageLines(cachedPage!);
            returnLines.AddRange(pageLines);
        }
        else
        {
            //  TODO - check dimensions are more then X and Y or its pointless
            ReadInStreamHeaders? textHeaders;

            try
            {
                await using var stream = new FileStream(imageFilepath, FileMode.Open);
                textHeaders = await _client.ReadInStreamAsync(stream);
            }
            catch (Exception ex)
            {
                if (ex is ComputerVisionOcrErrorException ocrEx)
                {
                    var errorCode = ocrEx.Response.Headers["ms-azure-ai-errorcode"].FirstOrDefault();

                    if (errorCode == "InvalidImageDimension")
                    {
                        await File.WriteAllTextAsync(
                            outputFilename,
                            JsonSerializer.Serialize(new ReadResult { Lines = [] },
                            JsonHelper.GetSerializerOptions()));
                        
                        return [];
                    }
                }
                
                if (!imageFilepath.Contains(".jpg", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw;
                }
                
                var bytAry = await File.ReadAllBytesAsync(imageFilepath);
                var deflated = PdfPigNoOcrImageService.Deflate(bytAry);

                var imageFilenameDeflated = imageFilepath.Replace(".jpg", "-deflated.jpg",
                    StringComparison.InvariantCultureIgnoreCase);
                await File.WriteAllBytesAsync(imageFilenameDeflated, deflated);

                await using var stream = new FileStream(imageFilenameDeflated, FileMode.Open);
                textHeaders = await _client.ReadInStreamAsync(stream);
            }
            
            const int waitBeforeCheck = 2000;
            await Task.Delay(waitBeforeCheck);

            // Retrieve the URI where the extracted text will be stored from the Operation-Location header.
            // We only need the ID and not the full URL
            const int numberOfCharsInOperationId = 36;

            var operationLocation = textHeaders.OperationLocation;
            var operationId = Guid.Parse(operationLocation[^numberOfCharsInOperationId..]);

            // Extract the text
            ReadOperationResult results;

            do
            {
                results = await _client.GetReadResultAsync(operationId);
            }
            while (results.Status is OperationStatusCodes.Running or OperationStatusCodes.NotStarted);
            
            if (results.AnalyzeResult.ReadResults.Count > 1)
            {
                throw new Exception("Cache is broken with more then one result");
            }
            
            Directory.CreateDirectory(folder);
            
            foreach (var page in results.AnalyzeResult.ReadResults)
            {
                await File.WriteAllTextAsync(outputFilename, JsonSerializer.Serialize(page, JsonHelper.GetSerializerOptions()));

                var pageLines = ToPageLines(page!);
                returnLines.AddRange(pageLines);
            }
        }
        
        var lineNumber = 0;
        
        (string Text, IList<Word> Words)? previousLine = null;
        var lineIndex = 0;
        
        // BoundingBox is { X top left, Y top left , X top right , Y top right,
        // X bottom right , Y bottom right , X bottom left , Y bottom left }
        
        return returnLines
            .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .Where(line => !DataHelper.IsCorruptedText(line.Text, 100))
            .GroupBy(line =>
            {
                previousLine ??= line;

                var yDiff =
                    line.Words[0].BoundingBox[1]
                    - previousLine.Value.Words[0].BoundingBox[1];
                
                const int lineHeight = 15;
                
                if (yDiff >= lineHeight)
                {
                    lineIndex += 1;
                }

                previousLine = line;
                return lineIndex;
            })
            .Select(lines =>
            {
                var columns = new List<DocumentLineColumn>();

                foreach (var line in lines.OrderBy(l => l.Words[0].BoundingBox[0]))
                {
                    columns.Add(new DocumentLineColumn(line.Item1, line.Words.Select(word =>
                        new DocumentLineWord(
                            word.Text,
                            word.Confidence * 100,
                            new DocumentLineWordCoordinates(
                                word.BoundingBox[1] ?? PositionConstants.UnknownCoordinate,
                                word.BoundingBox[2] ?? PositionConstants.UnknownCoordinate,
                                word.BoundingBox[3] ?? PositionConstants.UnknownCoordinate,
                                word.BoundingBox[0] ?? PositionConstants.UnknownCoordinate)))
                    .ToList())
                    );
                }
                
                var documentLine = new DocumentLine(
                    lineNumber++,
                    pageNumber,
                    columns,
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate,
                    PositionConstants.UnknownCoordinate);

                return documentLine;
            })
            .ToList();
    }

    private static IEnumerable<(string, IList<Word>)> ToPageLines(ReadResult page)
    {
        const int roundTo = 40;
        
        var pageLines = page.Lines
            .OrderBy(line => LineSnappingHelper.RoundToNearestN(line.BoundingBox[3]!.Value, roundTo, line.Text))
            .ThenBy(line => line.BoundingBox[0]!.Value);

        // TODO add grouping and ordering
        
        return pageLines.Select(line => (line.Text, line.Words));
    }
    
    private static ComputerVisionClient Authenticate(string endpoint, string key)
    {
        return new ComputerVisionClient(
            new ApiKeyServiceClientCredentials(key))
            {
                Endpoint = endpoint
            };
    }
    

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}