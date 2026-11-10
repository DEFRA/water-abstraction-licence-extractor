using System.Net;
using System.Text.Json;
using WALE.ProcessFile.Core.Models.NoOcrService;
using WALE.ProcessFile.Services.Cache;
using Xunit;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

public class ApiCacheServiceBatchedPageTextTests
{
    private sealed class RecordingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }

    private static ApiCacheService ServiceFor(RecordingHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") });

    private static List<(NoOcrServicePageCacheRequest, string)> Pages(int count, int bytesEach)
    {
        var fileId = Guid.NewGuid();

        return Enumerable.Range(1, count)
            .Select(page => (
                new NoOcrServicePageCacheRequest
                {
                    FileId = fileId,
                    PageNumber = page,
                    NoOcrServiceName = "PdfPig",
                    ProcessRunId = 42
                },
                new string('x', bytesEach)))
            .ToList();
    }

    private static int PagesInBody(string body) =>
        JsonDocument.Parse(body).RootElement.GetProperty("pages").GetArrayLength();

    [Fact]
    public async Task AWholeDocumentCostsOneRequest()
    {
        var handler = new RecordingHandler();

        await ServiceFor(handler).SaveNoOcrPagesTextLinesAsync(Pages(20, 50_000));

        Assert.Single(handler.Bodies);
        Assert.Equal(20, PagesInBody(handler.Bodies[0]));
    }

    [Fact]
    public async Task ABatchOverTheSizeBudgetIsSplitAndNoPageIsLost()
    {
        var handler = new RecordingHandler();

        // 6 x 1MB against a 4MB budget
        await ServiceFor(handler).SaveNoOcrPagesTextLinesAsync(Pages(6, 1024 * 1024));

        Assert.Equal(2, handler.Bodies.Count);
        Assert.Equal(6, handler.Bodies.Sum(PagesInBody));
    }

    [Fact]
    public async Task OnePageLargerThanTheBudgetIsStillSent()
    {
        var handler = new RecordingHandler();

        await ServiceFor(handler).SaveNoOcrPagesTextLinesAsync(Pages(1, 5 * 1024 * 1024));

        Assert.Single(handler.Bodies);
        Assert.Equal(1, PagesInBody(handler.Bodies[0]));
    }

    [Fact]
    public async Task NothingToSaveSendsNoRequest()
    {
        var handler = new RecordingHandler();

        await ServiceFor(handler).SaveNoOcrPagesTextLinesAsync([]);

        Assert.Empty(handler.Bodies);
    }

    [Fact]
    public async Task EveryPageNumberSurvivesTheBatch()
    {
        var handler = new RecordingHandler();

        await ServiceFor(handler).SaveNoOcrPagesTextLinesAsync(Pages(9, 700 * 1024));

        var pageNumbers = handler.Bodies
            .SelectMany(body => JsonDocument.Parse(body).RootElement
                .GetProperty("pages").EnumerateArray()
                .Select(page => page.GetProperty("pageNumber").GetInt32()))
            .OrderBy(number => number)
            .ToList();

        Assert.Equal(Enumerable.Range(1, 9), pageNumbers);
    }
}
