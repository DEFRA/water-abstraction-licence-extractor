using System.Text.Json;
using Amazon;
using Amazon.Textract;
using Amazon.Textract.Model;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Models;
using WALE.ProcessFile.Models.Interfaces;
using WALE.ProcessFile.Models.OutputSchema;

namespace WALE.ProcessFile.Services.AwsTextract;

public class AwsTextractOcrDataExtractorService(
    string accessKey,
    string secretKey,
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
        
        const int lineHeight = 12;
        const int wordGap = 100;
        const int minWordHeight = 5;
        
        return OcrHelper.Group(returnLines, pageNumber, lineHeight, wordGap, minWordHeight);
    }
    
    private async Task<List<LineAndWords>> GetDataFromTextractAsync(byte[] bytes)
    {
        try
        {
            var stream = new MemoryStream(bytes);
            
            var awsCredentials = new Amazon.Runtime.BasicAWSCredentials(accessKey, secretKey);
            var client = new AmazonTextractClient(awsCredentials, RegionEndpoint.EUWest2);

            var analyzeDocumentRequest = new AnalyzeDocumentRequest
            {
                Document = new Document
                {
                    Bytes = stream
                },
                FeatureTypes = [FeatureType.FORMS]
            };

            const double coordinatesFormatMultiplier = 1000.0;
            
            var analyzeDocumentResponse = await client.AnalyzeDocumentAsync(analyzeDocumentRequest);
            var returnList = new List<LineAndWords>();
            
            foreach (var block in analyzeDocumentResponse.Blocks)
            {
                if (block.Text == null)
                {
                    continue;
                }
                
                var line = new LineAndWords
                {
                    Words = new List<DocumentLineWord>
                    {
                        new(
                            block.Text,
                            block.Confidence,
                            new DocumentLineWordCoordinates(
                                (block.Geometry.BoundingBox.Top ?? -1.0) * coordinatesFormatMultiplier,
                                    (block.Geometry.BoundingBox.Left + block.Geometry.BoundingBox.Width ?? -1.0) * coordinatesFormatMultiplier,
                                    (block.Geometry.BoundingBox.Top + block.Geometry.BoundingBox.Height ?? -1.0) * coordinatesFormatMultiplier,
                                (block.Geometry.BoundingBox.Left ?? -1.0) * coordinatesFormatMultiplier
                            )
                        )
                    }!
                };

                line.Text = string.Join(" ", line.Words.Select(w => w!.Text));
                returnList.Add(line);
            }

            return returnList;
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            throw;
        }
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}