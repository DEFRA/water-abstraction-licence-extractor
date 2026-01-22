using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.Textract;
using Amazon.Textract.Model;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OutputSchema;

namespace WALE.ProcessFile.Services.AwsTextract;

public class AwsTextractOcrDataExtractorService
    : IOcrDataExtractorService, IDisposable
{
    private static AwsTextractOcrDataExtractorService? _instance;
    
    private AwsTextractOcrDataExtractorService(
        string accessKey,
        string secretKey,
        ICacheService cacheService,
        IOutputService outputService)
    {
        _accessKey = accessKey;
        _secretKey = secretKey;
        _cacheService = cacheService;
        _outputService = outputService;
    }

    public static AwsTextractOcrDataExtractorService Instance(
        string accessKey,
        string secretKey,
        ICacheService cacheService,
        IOutputService outputService)
    {
        if (_instance != null)
        {
            return _instance;
        }
        
        _instance = new AwsTextractOcrDataExtractorService(
            accessKey,
            secretKey,
            cacheService,
            outputService);
        
        return _instance;
    }

    private readonly string _accessKey;
    private readonly string _secretKey;
    private readonly ICacheService _cacheService;
    private readonly IOutputService _outputService;
    
    public bool HasDirectCost => false;
    public string Name => "AwsTextractOcrDataExtractorService";
    
    private static readonly Lock ClientInitialisationLock = new();

    private const int MaxRequestsPerSecond = 1;
    private static readonly SemaphoreSlim RequestLock = new(1, MaxRequestsPerSecond);

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
            ? await _cacheService.GetOcrScreenshotTextAsync(request)
            : await _cacheService.GetOcrImageTextAsync(request);
        
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
            
            if (isPageScreenshot)
            {
                bytes = await _outputService.GetPageScreenshotDataAsync(
                    pageNumber,
                    noOcrServiceName,
                    pdfFilepath);
            }
            else
            {
                bytes = await _cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
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

            try
            {
                returnLines = await GetDataFromTextractAsync(bytes);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            if (isPageScreenshot)
            {
                await _cacheService.SaveOcrScreenshotTextAsync(request, returnLines);                
            }
            else
            {
                await _cacheService.SaveOcrImageTextAsync(request, returnLines);                
            }
        }
        
        const int horizontalColumnGap = 100;
        const int minFontSize = 5; // Can't really go lower, this is tiny
        const int considerableOverlapAmount = 3;
        
        return OcrHelper.Group(
            returnLines,
            true,
            pageNumber,
            horizontalColumnGap,
            minFontSize,
            considerableOverlapAmount);
    }
    
    private static async Task<AnalyzeDocumentResponse> AnalyzeDocumentAsync(AmazonTextractClient client, AnalyzeDocumentRequest analyzeDocumentRequest)
    {
        await RequestLock.WaitAsync();

        try
        {
            return await client.AnalyzeDocumentAsync(analyzeDocumentRequest);
        }
        finally
        {
            RequestLock.Release();
        }
    }
    
    private static AmazonTextractClient? _client;
    
    private static AmazonTextractClient GetTextractClient(string accessKey, string secretKey)
    {
        lock (ClientInitialisationLock)
        {
            if (_client != null)
            {
                return _client;
            }

            var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
            var client = new AmazonTextractClient(
                awsCredentials,
                new AmazonTextractConfig
                {
                    RetryMode = RequestRetryMode.Standard,
                    MaxErrorRetry = 5,
                    RegionEndpoint = RegionEndpoint.EUWest2
                });

            _client = client;
            return client;
        }
    }
    
    private async Task<List<LineAndWords>> GetDataFromTextractAsync(byte[] bytes)
    {
        var stream = new MemoryStream(bytes);
        
        var analyzeDocumentRequest = new AnalyzeDocumentRequest
        {
            Document = new Document
            {
                Bytes = stream
            },
            FeatureTypes = [FeatureType.FORMS]
        };

        const double coordinatesFormatMultiplier = 1_000.0;

        var client = GetTextractClient(_accessKey, _secretKey);
        var analyzeDocumentResponse = await AnalyzeDocumentAsync(client, analyzeDocumentRequest);
        
        var returnList = new List<LineAndWords>();
        
        foreach (var block in analyzeDocumentResponse.Blocks)
        {
            if (block.BlockType != BlockType.WORD)
            {
                continue;
            }
            
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
                            (block.Geometry.Polygon[0].Y ?? -1.0) * coordinatesFormatMultiplier,
                               (block.Geometry.Polygon[1].X ?? -1.0) * coordinatesFormatMultiplier,
                                (block.Geometry.Polygon[2].Y ?? -1.0) * coordinatesFormatMultiplier,
                            (block.Geometry.Polygon[3].X ?? -1.0) * coordinatesFormatMultiplier
                        ),
                        block.TextType.Value
                    )
                }!
            };

            returnList.Add(line);
        }

        return returnList;
    }
    
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }
}