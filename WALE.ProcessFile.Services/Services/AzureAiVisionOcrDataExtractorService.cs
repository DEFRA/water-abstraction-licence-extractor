using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Constants;
using WALE.ProcessFile.Services.Helpers;
using WALE.ProcessFile.Services.Interfaces;
using WALE.ProcessFile.Services.Models;
using WALE.ProcessFile.Services.Services.PdfPig;

namespace WALE.ProcessFile.Services.Services;

public class AzureAiVisionOcrDataExtractorService(
    string endpoint,
    string key,
    ICacheService cacheService) : IOcrDataExtractorService
{
    public bool HasDirectCost => true;
    public string Name => "AzureAiVisionOcr";

    private readonly ComputerVisionClient _client = Authenticate(endpoint, key);

    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(string imageReference, string pdfFilepath, int pageNumber, int imageNumber, PdfDocument pdfDocument, int processRunId)
    {
        var returnLines = new List<(string Text, IList<Word> Words)>();
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
            var cachedPage = JsonSerializer.Deserialize<ReadResult>(
                cacheFileText,
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
                var bytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
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
                
                await using var stream = new MemoryStream(bytes);
                textHeaders = await _client.ReadInStreamAsync(stream);
            }
            catch (Exception ex)
            {
                if (ex is ComputerVisionOcrErrorException ocrEx)
                {
                    var errorCode = ocrEx.Response.Headers["ms-azure-ai-errorcode"].FirstOrDefault();

                    if (errorCode == "InvalidImageDimension")
                    {
                        var data = JsonSerializer.Serialize(new ReadResult { Lines = [] },
                            JsonHelper.GetSerializerOptions());

                        await cacheService.SaveOcrImageTextAsync(request, data);
                        return [];
                    }
                }
                
                if (!imageReference.Contains(".jpg", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw;
                }
                
                var bytes = await cacheService.SaveDeflatedImageAsync(
                    request.Filepath,
                    request.ImageNumber,
                    request.PageNumber);

                await using var stream = new MemoryStream(bytes);
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
                throw new Exception("Cache is broken with more then one result - generally the result of passing in a PDF rather then an image");
            }
            
            foreach (var page in results.AnalyzeResult.ReadResults)
            {
                var data = JsonSerializer.Serialize(page, JsonHelper.GetSerializerOptions());
                await cacheService.SaveOcrImageTextAsync(request, data);
                
                var pageLines = ToPageLines(page!);
                returnLines.AddRange(pageLines);
            }
        }

        var returnLinesInFormat = returnLines
            .Select(l => new LineAndWords
            {
                Text = l.Text,
                Words = l.Words.Select(WordToDocumentLineWord).ToList()!
            })
            .ToList();
        
        const int lineHeight = 18;
        const int wordGap = 200;
        
        return OcrHelper.Group(returnLinesInFormat, pageNumber, lineHeight, wordGap);
    }

    private static DocumentLineWord WordToDocumentLineWord(Word word)
    {
        return new DocumentLineWord(
            word.Text,
            word.Confidence * 100,
            new DocumentLineWordCoordinates(
                word.BoundingBox[1] ?? PositionConstants.UnknownCoordinate, 
                word.BoundingBox[2] ?? PositionConstants.UnknownCoordinate, 
                word.BoundingBox[3] ?? PositionConstants.UnknownCoordinate, 
                word.BoundingBox[0] ?? PositionConstants.UnknownCoordinate));
    }
    
    private static IEnumerable<(string, IList<Word>)> ToPageLines(ReadResult page)
    {
        const int roundTo = 40;
        
        var pageLines = page.Lines
            .OrderBy(line => LineSnappingHelper.RoundToNearestN(line.BoundingBox[3]!.Value, roundTo, line.Text))
            .ThenBy(line => line.BoundingBox[0]!.Value);
        
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