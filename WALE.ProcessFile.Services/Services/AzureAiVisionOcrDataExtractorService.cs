using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using WALE.ProcessFile.Services.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;

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
                JsonHelper.GetSerializer());

            var pageLines = ToPageLines(cachedPage!);
            returnLines.AddRange(pageLines);
        }
        else
        {
            //  TODO - check dimensions are more then X and Y or its pointless
            
            await using var stream = new FileStream(imageFilepath, FileMode.Open);
            var textHeaders = await _client.ReadInStreamAsync(stream);

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
            
            Directory.CreateDirectory(folder);
            
            foreach (var page in results.AnalyzeResult.ReadResults)
            {
                await File.WriteAllTextAsync(outputFilename, JsonSerializer.Serialize(page, JsonHelper.GetSerializer()));

                var pageLines = ToPageLines(page!);
                returnLines.AddRange(pageLines);
            }
        }

        var lineNumber = 0;
        
        return returnLines
            .Where(line => !FormattingHelper.IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .Select(line => (FormattingHelper.Standardise(line.Text), line.Words))
            .Select(line => new DocumentLine(
                line.Item1,
                lineNumber++,
                pageNumber,
                line.Words.Select(word =>
                    new DocumentLineWord(
                        word.Text,
                        word.Confidence * 100,
                        new DocumentLineWordCoordinates(
                            word.BoundingBox[0] ?? PositionConstants.UnknownCoordinate,
                            word.BoundingBox[1] ?? PositionConstants.UnknownCoordinate,
                            word.BoundingBox[2] ?? PositionConstants.UnknownCoordinate,
                            word.BoundingBox[3] ?? PositionConstants.UnknownCoordinate)))
                    .ToList(),
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate,
                PositionConstants.UnknownCoordinate))            
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