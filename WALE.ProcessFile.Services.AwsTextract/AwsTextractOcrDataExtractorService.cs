using RateLimiter;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.Textract;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.Textract.Model;
using WALE.ProcessFile.Core.Enums;
using WALE.ProcessFile.Core.Exceptions;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models.OcrService;

namespace WALE.ProcessFile.Services.AwsTextract;

public class AwsTextractOcrDataExtractorService
    : IOcrDataExtractorService, IDisposable
{
    private static AwsTextractOcrDataExtractorService? _instance;
    
    private AwsTextractOcrDataExtractorService(
        string? accessKey,
        string? secretKey,
        string apiBaseUrl,
        ICacheService cacheService,
        IOutputService outputService)
    {
        _accessKey = accessKey;
        _secretKey = secretKey;
        _apiBaseUrl = apiBaseUrl;
        _cacheService = cacheService;
        _outputService = outputService;
    }
    
    // Best practice (TODO confirm this) because of rate limiting etc is to use a singleton instance
    public static AwsTextractOcrDataExtractorService Instance(
        string? accessKey,
        string? secretKey,
        string apiBaseUrl,
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
            apiBaseUrl,
            cacheService,
            outputService);
        
        return _instance;
    }
    
    private readonly ICacheService _cacheService;
    private readonly IOutputService _outputService;
    
    private readonly string _apiBaseUrl;

    // These 2 properties are only set in a certain auth mode
    private readonly string? _accessKey;
    private readonly string? _secretKey;
    
    public bool HasDirectCost => true;

    public OcrServiceFailurePolicy FailurePolicy => OcrServiceFailurePolicy.FailFileOnTransientError;
    public string Name => "AwsTextractOcrDataExtractorService";
    
    private static readonly Lock ClientInitialisationLock = new();

    // Account quotas divided by the Lambda's reserved concurrency, because the limiters are per
    // instance and every instance calls Textract. Raise these only with the quotas: over them,
    // pages fail OCR and the file still reports success. The synchronous DetectDocumentText is
    // 1/sec, which is why this uses the asynchronous operations instead.
    private const int AccountStartRequestsPerSecond = 15;
    private const int AccountPollRequestsPerSecond = 20;
    private const int AccountConcurrentJobs = 100;
    private const int ReservedConcurrency = 10;

    private static readonly TimeLimiter StartLimiter = TimeLimiter.GetFromMaxCountByInterval(
        1,
        TimeSpan.FromSeconds((double)ReservedConcurrency / AccountStartRequestsPerSecond));

    private static readonly TimeLimiter PollLimiter = TimeLimiter.GetFromMaxCountByInterval(
        1,
        TimeSpan.FromSeconds((double)ReservedConcurrency / AccountPollRequestsPerSecond));

    // LimitExceededException rather than queueing, so the ceiling needs holding here.
    private static readonly SemaphoreSlim JobsInFlight =
        new(AccountConcurrentJobs / ReservedConcurrency);

    private const string TempPrefix = "textract-ocr/";
    // A one-image job took ~25s when measured, and a hung one holds a worker, so this is a few
    // times that rather than generous.
    private static readonly TimeSpan JobTimeout = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan PollEvery = TimeSpan.FromSeconds(2);

    private static readonly SemaphoreSlim BucketLock = new(1, 1);
    private static string? _assetsBucket;

    /// The function has no bucket of its own - AwsS3BucketName is not set on it and
    /// ApiFileService reports "N/A" - so the name comes from the API, once per instance.
    private async Task<string> GetAssetsBucketAsync()
    {
        if (_assetsBucket != null)
        {
            return _assetsBucket;
        }

        await BucketLock.WaitAsync();

        try
        {
            if (_assetsBucket != null)
            {
                return _assetsBucket;
            }

            using var httpClient = new HttpClient { BaseAddress = new Uri(_apiBaseUrl) };
            var response = await httpClient.GetAsync("/Extractor/Files/GetBuckets");

            // Transient rather than swallowed: without a bucket there is no OCR, and the file
            // saving as a success without it is the failure this provider exists to avoid.
            if (!response.IsSuccessStatusCode)
            {
                throw new OcrServiceTransientException(
                    nameof(AwsTextract),
                    $"{(int)response.StatusCode} asking the API for the bucket names",
                    new HttpRequestException($"{(int)response.StatusCode}"));
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());

            _assetsBucket = document.RootElement.GetProperty("assets").GetString()
                ?? throw new InvalidOperationException("The API returned no assets bucket name");

            return _assetsBucket;
        }
        finally
        {
            BucketLock.Release();
        }
    }

    private async Task UploadAsync(string bucket, string key, byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);

        await GetS3Client().PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = key,
            InputStream = stream
        });
    }

    private async Task DeleteAsync(string bucket, string key)
    {
        try
        {
            await GetS3Client().DeleteObjectAsync(new DeleteObjectRequest
            {
                BucketName = bucket,
                Key = key
            });
        }
        catch (Exception exception)
        {
            // The page has its text; losing the temp object to a lifecycle rule is the lesser harm.
            ConsoleHelper.WriteLine($"WARNING - {nameof(AwsTextract)} - could not delete {key}"
                + $" - {exception.Message}");
        }
    }

    private static readonly Lock S3InitialisationLock = new();
    private static IAmazonS3? _s3Client;

    private IAmazonS3 GetS3Client()
    {
        lock (S3InitialisationLock)
        {
            if (_s3Client != null)
            {
                return _s3Client;
            }

            _s3Client = string.IsNullOrEmpty(_accessKey)
                ? new AmazonS3Client(RegionEndpoint.EUWest2)
                : new AmazonS3Client(
                    new BasicAWSCredentials(_accessKey, _secretKey),
                    RegionEndpoint.EUWest2);

            return _s3Client;
        }
    }

    public async Task<IReadOnlyList<DocumentLine>>
        GetTextLinesFromImageAsync(
            string imageReference,
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
            FileId = pdfDocument.FileId,
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
                    pdfDocument.FileId);
            }
            else
            {
                var bytes = await _cacheService.GetImageBytesAsync(new OcrServiceImageDataCacheRequest
                {
                    PageNumber = pageNumber,
                    ImageNumber = imageNumber,
                    FileId = pdfDocument.FileId,
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
            catch (AmazonServiceException e) when (IsTransient(e))
            {
                ConsoleHelper.WriteLine($"ERROR - {nameof(AwsTextract)} - transient failure (SDK retries exhausted) - {e}");

                throw new OcrServiceTransientException(
                    Name,
                    $"{Name} transient failure for P{pageNumber}, I{imageNumber}, {pdfDocument.FileId}: " +
                    $"{e.GetType().Name} - {e.Message}",
                    e);
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
    
    /// <summary>
    /// Errors that are expected to clear on their own. The SDK has already retried these
    /// (RetryMode.Standard, MaxErrorRetry = 5) by the time they surface here, so reaching this
    /// point means sustained throttling or a service-side outage, not a blip.
    /// </summary>
    private static bool IsTransient(AmazonServiceException exception)
    {
        if (exception is ProvisionedThroughputExceededException
            or ThrottlingException
            or LimitExceededException
            or InternalServerErrorException)
        {
            return true;
        }

        if (exception.Retryable is { Throttling: true })
        {
            return true;
        }

        return (int)exception.StatusCode >= 500;
    }

    /// The asynchronous operations only read from S3 - DocumentLocation has no inline bytes - so
    /// the image goes to a temp object for the length of the job.
    private async Task<List<Block>> DetectDocumentTextAsync(AmazonTextractClient client, byte[] bytes)
    {
        var bucket = await GetAssetsBucketAsync();

        // ClientRequestToken takes ^[a-zA-Z0-9-_]+$, so it is the id alone - the key's prefix
        // carries a slash, which S3 accepts and Textract rejects.
        var id = Guid.NewGuid().ToString("N");
        var key = TempPrefix + id;

        await UploadAsync(bucket, key, bytes);

        try
        {
            await JobsInFlight.WaitAsync();

            try
            {
                var jobId = await StartJobAsync(client, bucket, key, id);
                return await CollectBlocksAsync(client, jobId);
            }
            finally
            {
                JobsInFlight.Release();
            }
        }
        finally
        {
            await DeleteAsync(bucket, key);
        }
    }

    private static async Task<string> StartJobAsync(
        AmazonTextractClient client,
        string bucket,
        string key,
        string requestToken)
    {
        var response = await StartLimiter.Enqueue(() => client.StartDocumentTextDetectionAsync(
            new StartDocumentTextDetectionRequest
            {
                // Same token for the same object, so an SQS redelivery joins the existing job
                // rather than starting a second one against the concurrent-jobs ceiling.
                ClientRequestToken = requestToken,
                DocumentLocation = new DocumentLocation
                {
                    S3Object = new Amazon.Textract.Model.S3Object { Bucket = bucket, Name = key }
                }
            }));

        return response.JobId;
    }

    private static async Task<List<Block>> CollectBlocksAsync(AmazonTextractClient client, string jobId)
    {
        var giveUpAt = DateTime.UtcNow + JobTimeout;
        var blocks = new List<Block>();
        string? nextToken = null;

        while (true)
        {
            var response = await PollLimiter.Enqueue(() => client.GetDocumentTextDetectionAsync(
                new GetDocumentTextDetectionRequest { JobId = jobId, NextToken = nextToken }));

            if (response.JobStatus == JobStatus.IN_PROGRESS)
            {
                if (DateTime.UtcNow > giveUpAt)
                {
                    throw new OcrServiceTransientException(
                        nameof(AwsTextract),
                        $"Textract job {jobId} still running after {JobTimeout.TotalSeconds:F0}s",
                        new TimeoutException());
                }

                await Task.Delay(PollEvery);
                continue;
            }

            if (response.JobStatus != JobStatus.SUCCEEDED)
            {
                throw new AmazonTextractException(
                    $"Textract job {jobId} finished {response.JobStatus}: {response.StatusMessage}");
            }

            blocks.AddRange(response.Blocks);
            nextToken = response.NextToken;

            if (string.IsNullOrEmpty(nextToken))
            {
                return blocks;
            }
        }
    }
    
    private static AmazonTextractClient? _client;
    
    private static AmazonTextractClient GetTextractClient(
        string? accessKey,
        string? secretKey)
    {
        lock (ClientInitialisationLock)
        {
            if (_client != null)
            {
                return _client;
            }

            var config = new AmazonTextractConfig
            {
                RetryMode = RequestRetryMode.Standard,
                MaxErrorRetry = 5,
                RegionEndpoint = RegionEndpoint.EUWest2
            };
            
            AmazonTextractClient client;
            
            if (!string.IsNullOrEmpty(accessKey))
            {
                var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);
                client = new AmazonTextractClient(awsCredentials, config);
            }
            else
            {
                client = new AmazonTextractClient(config);
            }
            
            _client = client;
            return client;
        }
    }
    
    private async Task<List<LineAndWords>> GetDataFromTextractAsync(byte[] bytes)
    {
        const double coordinatesFormatMultiplier = 1_000.0;

        var client = GetTextractClient(_accessKey, _secretKey);
        var blocks = await DetectDocumentTextAsync(client, bytes);
        
        var returnList = new List<LineAndWords>();
        
        foreach (var blockWord in blocks)
        {
            if (blockWord.BlockType != BlockType.WORD)
            {
                continue;
            }
            
            if (blockWord.Text == null)
            {
                continue;
            }

            var top = (blockWord.Geometry.Polygon[0].Y ?? -1.0) * coordinatesFormatMultiplier;
            var bottom = (blockWord.Geometry.Polygon[2].Y ?? -1.0) * coordinatesFormatMultiplier;

            var origLeft = (blockWord.Geometry.Polygon[3].X ?? -1.0) * coordinatesFormatMultiplier;
            var origRight = (blockWord.Geometry.Polygon[1].X ?? -1.0) * coordinatesFormatMultiplier;
            
            var individualWords = blockWord.Text.Split(' ');
            var totalWidth = origRight - origLeft;
            var widthPerWord = totalWidth / individualWords.Length;
            const int dividerWidth = 10;
            
            var words = individualWords
                .Select((wordText, index) => (DocumentLineWord?)new DocumentLineWord(
                    wordText,
                    blockWord.Confidence,
                    new DocumentLineWordCoordinates(
                        top,
                        origLeft + ((index + 1) * widthPerWord),
                        bottom,
                        origLeft + (index * widthPerWord) + (index > 0 ? dividerWidth : 0)
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