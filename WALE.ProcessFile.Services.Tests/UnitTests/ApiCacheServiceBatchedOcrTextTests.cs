using System.Net;
using System.Text.Json;
using WALE.ProcessFile.Core.Models.OcrService;
using WALE.ProcessFile.Services.Cache;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

public class ApiCacheServiceBatchedOcrTextTests
{
    private sealed class CountingHandler(Dictionary<string, string> allText) : HttpMessageHandler
    {
        public List<string> Paths { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Paths.Add(request.RequestUri!.PathAndQuery);

            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(allText))
            });
        }
    }

    private static ApiCacheService ServiceFor(CountingHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") });

    private static OcrServiceImageTextCacheRequest ImageRequest(Guid fileId, int page, int image) =>
        new()
        {
            FileId = fileId,
            PageNumber = page,
            ImageNumber = image,
            OcrServiceName = "TesseractOcr-SparseTextOsd",
            ProcessRunId = 42
        };

    [Fact]
    public async Task EveryPageAndImageOfOneFileCostsASingleRequest()
    {
        var fileId = Guid.NewGuid();
        var stored = new Dictionary<string, string>();

        for (var page = 1; page <= 5; page++)
        {
            for (var image = 0; image < 3; image++)
            {
                stored[OcrServiceAllTextCacheRequest.ImageKey(page, image)] = $"text-{page}-{image}";
            }
        }

        var handler = new CountingHandler(stored);
        var service = ServiceFor(handler);

        for (var page = 1; page <= 5; page++)
        {
            for (var image = 0; image < 3; image++)
            {
                var text = await service.GetOcrImageTextAsync(ImageRequest(fileId, page, image));

                Assert.Equal($"text-{page}-{image}", text);
            }
        }

        Assert.Single(handler.Paths);
        Assert.Contains("/Extractor/Ocr/GetAllText", handler.Paths[0]);
        Assert.Contains(fileId.ToString(), handler.Paths[0]);
    }

    [Fact]
    public async Task ScreenshotTextIsServedFromTheSameFetchAsImageText()
    {
        var fileId = Guid.NewGuid();
        var handler = new CountingHandler(new Dictionary<string, string>
        {
            [OcrServiceAllTextCacheRequest.ImageKey(2, 1)] = "image-text",
            [OcrServiceAllTextCacheRequest.ScreenshotKey(2)] = "screenshot-text"
        });

        var service = ServiceFor(handler);

        Assert.Equal("image-text", await service.GetOcrImageTextAsync(ImageRequest(fileId, 2, 1)));
        Assert.Equal("screenshot-text", await service.GetOcrScreenshotTextAsync(ImageRequest(fileId, 2, 0)));

        Assert.Single(handler.Paths);
    }

    [Fact]
    public async Task AMissingEntryIsNullRatherThanAFailure()
    {
        var handler = new CountingHandler([]);
        var service = ServiceFor(handler);

        Assert.Null(await service.GetOcrImageTextAsync(ImageRequest(Guid.NewGuid(), 9, 9)));
    }

    [Fact]
    public async Task ADifferentFileIsFetchedSeparately()
    {
        var handler = new CountingHandler([]);
        var service = ServiceFor(handler);

        await service.GetOcrImageTextAsync(ImageRequest(Guid.NewGuid(), 1, 0));
        await service.GetOcrImageTextAsync(ImageRequest(Guid.NewGuid(), 1, 0));

        Assert.Equal(2, handler.Paths.Count);
    }

    [Fact]
    public async Task ADifferentOcrServiceIsFetchedSeparately()
    {
        var fileId = Guid.NewGuid();
        var handler = new CountingHandler([]);
        var service = ServiceFor(handler);

        var first = ImageRequest(fileId, 1, 0);
        var second = ImageRequest(fileId, 1, 0);
        second.OcrServiceName = "AwsTextractOcrDataExtractorService";

        await service.GetOcrImageTextAsync(first);
        await service.GetOcrImageTextAsync(second);

        Assert.Equal(2, handler.Paths.Count);
    }
}
