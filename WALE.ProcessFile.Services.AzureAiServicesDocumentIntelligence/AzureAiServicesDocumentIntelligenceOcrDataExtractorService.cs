using System.Text.Json;
using Azure;
using Azure.AI.DocumentIntelligence;
using WALE.ProcessFile.Core.Constants;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.OutputSchema;
using WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence.Models;
using DocumentLine = Azure.AI.DocumentIntelligence.DocumentLine;

namespace WALE.ProcessFile.Services.AzureAiServicesDocumentIntelligence;

public class AzureAiServicesDocumentIntelligenceOcrDataExtractorService(
    string endpoint,
    string key,
    ICacheService cacheService,
    IOutputService outputService,
    int id = -1) : IOcrDataExtractorService
{
    public bool HasDirectCost => true;
    public string Name => "AzureAiServicesDocumentIntelligenceOcr";
    public int Id { get; set; } = id;

    private readonly DocumentIntelligenceClient _client = CreateClient(endpoint, key);

    public async Task<IReadOnlyList<WALE.ProcessFile.Core.Models.DocumentLine>> GetTextLinesFromImageAsync(
        string imageReference,
        string pdfFilepath,
        int pageNumber,
        int imageNumber,
        PdfDocument pdfDocument,
        int processRunId,
        string noOcrServiceName)
    {
        var isPageScreenshot = OcrHelper.IsPageScreenshot(imageReference, pageNumber);
        var returnLines = new List<DocumentIntelligenceLineWithWords>();
        
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
            var pageLines = JsonSerializer.Deserialize<List<DocumentIntelligenceLineWithWords>>(
                cacheFileText,
                JsonHelper.GetSerializerOptions());

            returnLines.AddRange(pageLines!);
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
            
            foreach (var bytes in bytesList)
            {
                var textLines = await GetTextLinesAsync(
                    bytes,
                    isPageScreenshot,
                    imageReference,
                    request);

                returnLines.AddRange(textLines);
            }
        }

        var returnLinesInFormat = returnLines
            .Select(l => new LineAndWords
            {
                Words = l.Words!.Select(WordToDocumentLineWord).ToList()!
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
            considerableOverlapAmount,
            multiplyConfidenceBy: 1); // TODO check
    }
    
    private async Task<List<DocumentIntelligenceLineWithWords>> GetTextLinesAsync(
        byte[] bytes,
        bool isPageScreenshot,
        string imageReference,
        OcrServiceImageTextCacheRequest request)
    {
        Operation<AnalyzeResult>? documentResult;
        
        try
        {
            var analyzeDocumentOptions = new AnalyzeDocumentOptions(
                "prebuilt-read",
                BinaryData.FromBytes(bytes));
            
            documentResult = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                analyzeDocumentOptions);
        }
        catch (Exception ex)
        {
            ConsoleHelper.WriteLine($"ERROR - {ex.GetType().Name} - {ex.Message}");
            
            if (ex is RequestFailedException ocrEx)
            {
                var errorCode = ocrEx.Message;

                if (errorCode.Contains("InvalidContentDimensions", StringComparison.InvariantCultureIgnoreCase))
                {
                    var dataEmpty = JsonSerializer.Serialize(
                        new List<DocumentIntelligenceLineWithWords>(),
                        JsonHelper.GetSerializerOptions());

                    if (isPageScreenshot)
                    {
                        await cacheService.SaveOcrScreenshotTextAsync(request, dataEmpty);                
                    }
                    else
                    {
                        await cacheService.SaveOcrImageTextAsync(request, dataEmpty);                
                    }
                        
                    return [];
                }

                throw;
            }
            
            if (!imageReference.Contains(".jpg", StringComparison.InvariantCultureIgnoreCase)
                && !imageReference.Contains("-jpg", StringComparison.InvariantCultureIgnoreCase))
            {
                throw;
            }
            
            bytes = await cacheService.DeflateImageAsync(
                request.Filepath!,
                request.ImageNumber,
                request.PageNumber,
                request.ProcessRunId,
                FileHelper.GetImageExtension(imageReference),
                GeneralConstants.PdfPigDataExtractorServiceName);

            var analyzeDocumentOptions = new AnalyzeDocumentOptions(
                "prebuilt-read",
                BinaryData.FromBytes(bytes));
            
            documentResult = await _client.AnalyzeDocumentAsync(
                WaitUntil.Completed,
                analyzeDocumentOptions);
        }

        var returnList = new List<DocumentIntelligenceLineWithWords>();
        var pageIndex = 1;
        
        foreach (var page in documentResult.Value.Pages)
        {
            var lineIndex = 1;
            
            foreach (var line in page.Lines)
            {
                var lineWithWords = new DocumentIntelligenceLineWithWords
                {
                    LineNumber = lineIndex++,
                    PageNumber = pageIndex,
                    Line = DeserialisableDocumentIntelligenceLine.FromDocumentLine(line),
                    Words = GetWordsForLine(line, page.Words)
                        .Select(DeserialisableDocumentIntelligenceWord.FromDocumentWord)
                        .ToList(),
                };
                
                returnList.Add(lineWithWords);
            }

            pageIndex += 1;
        }
        
        var data = JsonSerializer.Serialize(returnList, JsonHelper.GetSerializerOptions());

        if (isPageScreenshot)
        {
            await cacheService.SaveOcrScreenshotTextAsync(request, data);
        }
        else
        {
            await cacheService.SaveOcrImageTextAsync(request, data);
        }

        return returnList;
    }

    private static List<DocumentWord> GetWordsForLine(DocumentLine line, IReadOnlyList<DocumentWord> allPageWords)
    {
        if (line.Spans.Count > 1)
        {
            throw new Exception("We need to implement multiple line spans.");
        }
        
        var lineSpan = line.Spans.First();
        var lineOffsetStart = lineSpan.Offset;
        var lineOffsetEnd = lineOffsetStart + lineSpan.Length;
        
        var returnList = new List<DocumentWord>();

        foreach (var word in allPageWords)
        {
            var wordOffsetStart = word.Span.Offset;
            var wordOffsetEnd = wordOffsetStart + word.Span.Length;

            if (wordOffsetStart >= lineOffsetStart && wordOffsetEnd <= lineOffsetEnd)
            {
                returnList.Add(word);
            }
        }
        
        return returnList;
    }
    
    private static DocumentLineWord WordToDocumentLineWord(DeserialisableDocumentIntelligenceWord word)
    {
        return new DocumentLineWord(
            word.Content!,
            word.Confidence * 100,
            new DocumentLineWordCoordinates(
                word.Polygon![1],
                word.Polygon[2],
                word.Polygon[5],
                word.Polygon[0]),
            null);
    }
    
    private static DocumentIntelligenceClient CreateClient(string endpoint, string key)
    {
        var credential = new AzureKeyCredential(key);
        return new DocumentIntelligenceClient(new Uri(endpoint), credential);
    }
    
    private class DocumentIntelligenceLineWithWords
    {
        public DeserialisableDocumentIntelligenceLine? Line { get; set; }
        
        public List<DeserialisableDocumentIntelligenceWord>? Words { get; set; }
        
        public int PageNumber { get; set; }
        
        public int LineNumber { get; set; }
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}