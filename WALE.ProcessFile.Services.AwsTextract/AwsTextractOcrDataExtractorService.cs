using System.Text.Json;
using Amazon.Textract;
using Amazon.Textract.Model;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Interfaces;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Services.AwsTextract;

public class AwsTextractOcrDataExtractorService(
    ICacheService cacheService,
    IOutputService outputService)
    : IOcrDataExtractorService, IDisposable
{
    public bool HasDirectCost => false;
    public string Name => "AwsTextractOcrDataExtractorService";

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
        var isPageScreenshot = imageReference.StartsWith("Screenshot");
        
        var returnLines = new List<LineAndWords>();
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
            var imageLines = JsonSerializer.Deserialize<List<LineAndWords>>(
                cacheFileText,
                JsonHelper.GetSerializerOptions());
            
            returnLines.AddRange(imageLines!);
        }
        else
        {
            byte[]? bytes;
            
            try
            {
                if (isPageScreenshot)
                {
                    bytes = await outputService.GetPageScreenshotDataAsync(
                        pageNumber,
                        noOcrServiceName,
                        pdfFilepath);
                }
                else
                {
                    bytes = await cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
                    {
                        PageNumber = pageNumber,
                        ImageNumber = imageNumber,
                        Filepath = pdfFilepath,
                        NoOcrServiceName = noOcrServiceName,
                        Extension = FileHelper.GetImageExtension(imageReference)
                    });
                }

                if (bytes == null)
                {
                    throw new Exception("Image was not found");
                }
            }
            catch
            {
                if (!imageReference.Contains(".jpg", StringComparison.InvariantCultureIgnoreCase))
                {
                    throw;
                }

                bytes = await cacheService.SaveDeflatedImageAsync(
                    request.Filepath,
                    request.ImageNumber,
                    request.PageNumber,
                    request.ProcessRunId);
            }

            try
            {
                returnLines = await GetDataFromTextractAsync(bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (isPageScreenshot)
            {
                await cacheService.SaveOcrScreenshotTextAsync(request, returnLines);                
            }
            else
            {
                await cacheService.SaveOcrImageTextAsync(request, returnLines);                
            }
        }
        
        const int lineHeight = 21;
        const int wordGap = 200;
        
        return OcrHelper.Group(returnLines, pageNumber, lineHeight, wordGap);
    }
    
    private async Task<List<LineAndWords>> GetDataFromTextractAsync(byte[] bytes)
    {
        try
        {
            var stream = new MemoryStream(bytes);
            var client = new AmazonTextractClient();

            var analyzeDocumentRequest = new AnalyzeDocumentRequest
            {
                Document = new Document
                {
                    Bytes = stream
                }
            };
            
            var analyzeDocumentResponse = await client.AnalyzeDocumentAsync(analyzeDocumentRequest);

            //Get the text blocks
            var blocks = analyzeDocumentResponse.Blocks;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return [];
        }

        return [];
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}