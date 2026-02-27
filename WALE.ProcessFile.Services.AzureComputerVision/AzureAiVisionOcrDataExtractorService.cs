using System.Text.Json;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision;
using Microsoft.Azure.CognitiveServices.Vision.ComputerVision.Models;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.AzureComputerVision;

public class AzureAiVisionOcrDataExtractorService(
    string endpoint,
    string key,
    ICacheService cacheService,
    IOutputService outputService,
    int id = -1) : IOcrDataExtractorService
{
    public bool HasDirectCost => true;
    public string Name => "AzureAiVisionOcr";
    public int Id { get; set; } = id;

    private readonly ComputerVisionClient _client = CreateClient(endpoint, key);

    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(
            string imageReference,
            string pdfFilepath,
            int pageNumber,
            int imageNumber,
            PdfDocument pdfDocument,
            int processRunId,
            string noOcrServiceName)
    {
        var isPageScreenshot = OcrHelper.IsPageScreenshot(imageReference, pageNumber);
        
        var returnLines = new List<(string Text, IList<Word> Words)>();
        var request = new OcrServiceImageTextCacheRequest
        {
            PageNumber = pageNumber,
            ImageNumber = imageNumber,
            Filepath = pdfFilepath,
            OcrServiceName = Name,
            ProcessRunId = processRunId
        };
        
        var cacheFileText = isPageScreenshot
            ? await cacheService.GetOcrScreenshotTextAsync(request)
            : await cacheService.GetOcrImageTextAsync(request);
        
        if (pdfDocument.FromCache && !string.IsNullOrEmpty(cacheFileText))
        {
            // Handle the case we didn't get a real result, so we made one up
            if (cacheFileText != "{}")
            {
                var cachedPage = JsonSerializer.Deserialize<ReadResult>(
                    cacheFileText,
                    JsonHelper.GetSerializerOptions());

                var pageLines = ToPageLines(cachedPage!);
                returnLines.AddRange(pageLines);
            }
        }
        else
        {
            List<byte[]> bytesList;

            if (isPageScreenshot)
            {
                bytesList = await outputService.GetPageScreenshotDataAsync(
                    pageNumber,
                    GeneralConstants.PdfPigDataExtractorServiceName,
                    pdfFilepath);
            }
            else
            {
                var bytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
                {
                    PageNumber = pageNumber,
                    ImageNumber = imageNumber,
                    Filepath = pdfFilepath,
                    NoOcrServiceName = GeneralConstants.PdfPigDataExtractorServiceName,
                    Extension = FileHelper.GetImageExtension(imageReference)
                });

                bytesList =
                [
                    bytes!
                ];
            }

            if (bytesList.Count == 0)
            {
                throw new Exception("Image was not found");
            }
            
            var maxNumberOfWords = -1;

            foreach (var bytes in bytesList)
            {
                var textLines = await GetTextLinesAsync(
                    bytes,
                    isPageScreenshot,
                    imageReference,
                    request);
                
                var numberOfWords = textLines.Sum(line => line.Words.Count);

                if (numberOfWords <= maxNumberOfWords)
                {
                    continue;
                }
                    
                maxNumberOfWords = numberOfWords;
                returnLines = textLines;
            }
        }
        
        var returnLinesInFormat = returnLines
            .Select(l => new LineAndWords
            {
                Words = l.Words.Select(WordToDocumentLineWord).ToList()!
            })
            .ToList();

        const int horizontalColumnGap = 150;
        const int minFontSize = 15;
        const int considerableOverlapAmount = 19;

        return await OcrHelper.GroupAsync(
            returnLinesInFormat,
            true,
            pageNumber,
            horizontalColumnGap,
            minFontSize,
            considerableOverlapAmount);
    }

    private async Task<List<(string Text, IList<Word> Words)>> GetTextLinesAsync(
        byte[] bytes,
        bool isPageScreenshot,
        string imageReference,
        OcrServiceImageTextCacheRequest request)
    {
        ReadInStreamHeaders? textHeaders;
        ConsoleHelper.WriteLine($"INFO - {nameof(AzureAiVisionOcrDataExtractorService)} - Calling for P{request.PageNumber}, I{request.ImageNumber}, {request.Filepath}");
        
        try
        {
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
                    ConsoleHelper.WriteLine($"INFO - {nameof(AzureAiVisionOcrDataExtractorService)} - Azure AI Vision reported back image dimensions were too small");
                    
                    var data = JsonSerializer.Serialize(new ReadResult { Lines = [] },
                        JsonHelper.GetSerializerOptions());

                    if (isPageScreenshot)
                    {
                        await cacheService.SaveOcrScreenshotTextAsync(request, data);                
                    }
                    else
                    {
                        await cacheService.SaveOcrImageTextAsync(request, data);                
                    }
                        
                    return [];
                }
                
                // Let invalid image try deflate
                if (errorCode != "InvalidImage")
                {
                    ConsoleHelper.WriteLine($"ERROR - {nameof(AzureAiVisionOcrDataExtractorService)} - {ex.Message}");
                    throw;
                }
            }
            
            if (!imageReference.Contains(".jpg", StringComparison.InvariantCultureIgnoreCase)
                && !imageReference.Contains("-jpg", StringComparison.InvariantCultureIgnoreCase))
            {
                throw;
            }

            try
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(AzureAiVisionOcrDataExtractorService)} - {ex.Message}");
                
                // Try deflate
                bytes = await cacheService.DeflateImageAsync(
                    request.Filepath!,
                    request.ImageNumber,
                    request.PageNumber,
                    request.ProcessRunId,
                    FileHelper.GetImageExtension(imageReference),
                    GeneralConstants.PdfPigDataExtractorServiceName);

                await using var stream = new MemoryStream(bytes);
                textHeaders = await _client.ReadInStreamAsync(stream);
            }
            catch (Exception e)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(AzureAiVisionOcrDataExtractorService)} - After deflate attempt, {e.Message}");

                if (isPageScreenshot)
                {
                    await cacheService.SaveOcrScreenshotTextAsync(request, "{}");
                }
                else
                {
                    await cacheService.SaveOcrImageTextAsync(request, "{}");
                }
                
                return [];
            }
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
        } while (results.Status is OperationStatusCodes.Running or OperationStatusCodes.NotStarted);

        if (results.AnalyzeResult.ReadResults.Count > 1)
        {
            throw new Exception(
                "Cache is broken with more then one result - generally the result of passing in a PDF rather then an image");
        }

        var returnLines = new List<(string Text, IList<Word> Words)>();

        foreach (var page in results.AnalyzeResult.ReadResults)
        {
            var data = JsonSerializer.Serialize(page, JsonHelper.GetSerializerOptions());

            if (isPageScreenshot)
            {
                await cacheService.SaveOcrScreenshotTextAsync(request, data);
            }
            else
            {
                await cacheService.SaveOcrImageTextAsync(request, data);
            }

            var pageLines = ToPageLines(page!);
            returnLines.AddRange(pageLines);
        }

        return returnLines;
    }
    
    private static DocumentLineWord WordToDocumentLineWord(Word word)
    {
        // See this post for visualisation of box https://learn.microsoft.com/en-us/answers/questions/776499/what-is-the-difference-between-the-boundingboxes-i
        
        return new DocumentLineWord(
            word.Text,
            word.Confidence * 100,
            new DocumentLineWordCoordinates(
                word.BoundingBox[1] ?? PositionConstants.UnknownCoordinate, 
                word.BoundingBox[2] ?? PositionConstants.UnknownCoordinate, 
                word.BoundingBox[5] ?? PositionConstants.UnknownCoordinate, 
                word.BoundingBox[0] ?? PositionConstants.UnknownCoordinate),
            null);
    }
    
    private static IEnumerable<(string, IList<Word>)> ToPageLines(ReadResult page)
    {
        const int roundTo = 40;
        
        var pageLines = page.Lines
            .OrderBy(line => LineSnappingHelper.RoundToNearestN(line.BoundingBox[5]!.Value, roundTo, line.Text))
            .ThenBy(line => line.BoundingBox[0]!.Value);
        
        return pageLines.Select(line => (line.Text, line.Words));
    }
    
    private static ComputerVisionClient CreateClient(string endpoint, string key)
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