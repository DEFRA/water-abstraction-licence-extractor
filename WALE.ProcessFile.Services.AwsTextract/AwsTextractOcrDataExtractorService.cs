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
        var isPageScreenshot = OcrHelper.IsPageScreenshot(imageReference, pageNumber);
        
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
            List<byte[]> bytesList;
            
            if (isPageScreenshot)
            {
                bytesList = await _outputService.GetPageScreenshotDataAsync(
                    pageNumber,
                    noOcrServiceName,
                    pdfFilepath);
            }
            else
            {
                var bytes = await _cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
                {
                    PageNumber = pageNumber,
                    ImageNumber = imageNumber,
                    Filepath = pdfFilepath,
                    NoOcrServiceName = noOcrServiceName,
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

            try
            {
                var maxNumberOfWords = -1;
                
                foreach (var bytes in bytesList)
                {
                    var returnList = await GetDataFromTextractAsync(bytes);
                    var numberOfWords = returnList.Sum(line => line.Words!.Count);

                    if (numberOfWords <= maxNumberOfWords)
                    {
                        continue;
                    }
                    
                    maxNumberOfWords = numberOfWords;
                    returnLines = returnList;
                }
            }
            catch (Exception e)
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(AwsTextract)} - {e}");
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

        return await OcrHelper.GroupAsync(
            returnLines,
            true,
            pageNumber,
            horizontalColumnGap,
            minFontSize,
            considerableOverlapAmount);
    }
    
    private static async Task<DetectDocumentTextResponse> DetectDocumentTextAsync(
        AmazonTextractClient client,
        DetectDocumentTextRequest detectDocumentTextRequest)
    {
        await RequestLock.WaitAsync();

        try
        {
            return await client.DetectDocumentTextAsync(detectDocumentTextRequest);
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
        
        var detectDocumentTextRequest = new DetectDocumentTextRequest
        {
            Document = new Document
            {
                Bytes = stream
            }
        };

        const double coordinatesFormatMultiplier = 1_000.0;

        var client = GetTextractClient(_accessKey, _secretKey);
        var detectDocumentTextResponse = await DetectDocumentTextAsync(client, detectDocumentTextRequest);
        
        var returnList = new List<LineAndWords>();
        
        foreach (var blockWord in detectDocumentTextResponse.Blocks)
        {
            if (blockWord.BlockType != BlockType.WORD)
            {
                continue;
            }
            
            if (blockWord.Text == null)
            {
                continue;
            }

            var words = blockWord.Text
                .Split(' ')
                .Select(wordText => (DocumentLineWord?)new DocumentLineWord(
                    wordText,
                    blockWord.Confidence,
                    new DocumentLineWordCoordinates(
                    (blockWord.Geometry.Polygon[0].Y ?? -1.0) * coordinatesFormatMultiplier,
                    (blockWord.Geometry.Polygon[1].X ?? -1.0) * coordinatesFormatMultiplier,
                    (blockWord.Geometry.Polygon[2].Y ?? -1.0) * coordinatesFormatMultiplier,
                    (blockWord.Geometry.Polygon[3].X ?? -1.0) * coordinatesFormatMultiplier
                    ),
                    blockWord.TextType.Value
                    ))
                .ToList();
            
            var line = new LineAndWords
            {
                Words = words
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