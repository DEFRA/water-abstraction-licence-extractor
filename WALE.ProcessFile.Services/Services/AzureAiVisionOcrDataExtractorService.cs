using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using static WALE.ProcessFile.Services.Helpers.DataHelpers;

namespace WALE.ProcessFile.Services.Services;

public class AzureAiVisionOcrDataExtractorService(string endpoint, string key) : IOcrDataExtractorService
{
    public bool HasDirectCost => true;
    public string Name => "AzureAiVisionOcr";

    private readonly ComputerVisionClient _client = Authenticate(endpoint, key);

    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(byte[] imageData, int pageNumber, int imageNumber, PdfDocument pdfDocument)
    {
        var lines = new List<(string Text, IList<Word> Words)>();

        var folder = $"{pdfDocument.OutputFolder}/{Name}/Text";
        Directory.CreateDirectory(folder);
        
        var outputFilename = $"{folder}/ocr-page-{pageNumber}-image-{imageNumber}.json";
        
        if (pdfDocument.FromCache && File.Exists(outputFilename))
        {
            var txt = await File.ReadAllTextAsync(outputFilename);
            var page = JsonSerializer.Deserialize<ReadResult>(txt);

            var pageLines = ToPageLines(page!);
            lines.AddRange(pageLines);
        }
        else
        {
            using var stream = new MemoryStream(imageData);
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
            
            foreach (var page in results.AnalyzeResult.ReadResults)
            {
                await File.WriteAllTextAsync(outputFilename, JsonSerializer.Serialize(page));

                var pageLines = ToPageLines(page!);
                lines.AddRange(pageLines);
            }
        }

        var lineNumber = 0;
        
        return lines
            .Where(line => !IsNullOrEmptyWhitespaceOrPunctuation(line.Text))
            .Select(line => (Standardise(line.Text), line.Words))
            .Select(line => new DocumentLine(
                line.Item1,
                lineNumber++,
                pageNumber,
                line.Words.Select(word =>
                    new DocumentLineWord(
                        word.Text,
                        word.Confidence * 100,
                        word.BoundingBox.ToList()))
                    .ToList()))            
            .ToList();
    }

    private static IEnumerable<(string, IList<Word>)> ToPageLines(ReadResult page)
    {
        const int roundTo = 40;
        
        var pageLines = page.Lines
            .OrderBy(x => RoundToNearestN(x.BoundingBox[3]!.Value, roundTo))
            .ThenBy(x => x.BoundingBox[0]!.Value);

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
    
    private static int RoundToNearestN(double value, double roundTo)
    {
        return (int)Math.Round(value / roundTo) * (int)roundTo;
    }

    public void Dispose()
    {
        // TODO
        GC.SuppressFinalize(this);
    }
}