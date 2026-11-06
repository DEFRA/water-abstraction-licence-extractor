using System.Net;
using System.Text.Json;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Output;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

public class ApiOutputServiceErrorReportingTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Body = request.Content == null
                ? null
                : await request.Content.ReadAsStringAsync(cancellationToken);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("7")
            };
        }
    }

    [Fact]
    public async Task ReportingAnErrorSendsTheMessageAndNotJustTheIds()
    {
        var handler = new CapturingHandler();

        var service = new ApiOutputService(new HttpClient(handler)
        {
            BaseAddress = new Uri("https://localhost/")
        });

        await service.ReportErrorProcessRunFileAsync(new ProcessRunFile
        {
            ProcessRunFileId = 7,
            ProcessRunId = 3,
            FileName = "14450000884__f261ab6c.pdf",
            ErrorMessage = "System.Net.Http.HttpRequestException: 429 (Too Many Requests)."
        });

        Assert.NotNull(handler.Body);

        var sent = JsonSerializer.Deserialize<JsonElement>(handler.Body);

        Assert.Equal(
            "System.Net.Http.HttpRequestException: 429 (Too Many Requests).",
            sent.GetProperty("errorMessage").GetString());
    }
}
