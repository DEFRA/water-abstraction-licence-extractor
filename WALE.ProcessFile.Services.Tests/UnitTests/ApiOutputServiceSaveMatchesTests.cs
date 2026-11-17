using System.Net;
using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Services.Output;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

public class ApiOutputServiceSaveMatchesTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Bodies.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("") };
        }
    }

    private sealed class SaveMatchesBody
    {
        public List<MatchToSave>? Matches { get; set; }
    }

    private static List<MatchToSave> Matches(int count) =>
        Enumerable.Range(0, count)
            .Select(i => new MatchToSave
            {
                MatchesResultId = 4000 + i,
                LabelName = $"label-{i}",
                LabelGroupName = "PointsTable",
                Data = new LabelGroupResult
                {
                    LabelGroupName = "PointsTable",
                    LabelStartPageNumber = i,
                    Text =
                    [
                        new DocumentLine
                        {
                            Columns =
                            [
                                new DocumentLineColumn([new DocumentLineWord { Text = $"value-{i}" }])
                            ]
                        }
                    ]
                }
            })
            .ToList();

    [Fact]
    public async Task EveryFieldOfEveryMatchSurvivesTheWire()
    {
        var handler = new CapturingHandler();
        var service = new ApiOutputService(
            new HttpClient(handler) { BaseAddress = new Uri("https://localhost/") });

        await service.SaveMatchesAsync(Matches(3));

        var sent = Assert.Single(handler.Bodies);
        var received = JsonSerializer.Deserialize<SaveMatchesBody>(
            sent,
            JsonHelper.GetSerializerOptions())!;

        Assert.Equal(3, received.Matches!.Count);

        for (var i = 0; i < 3; i++)
        {
            var match = received.Matches[i];

            Assert.Equal(4000 + i, match.MatchesResultId);
            Assert.Equal($"label-{i}", match.LabelName);
            Assert.Equal("PointsTable", match.LabelGroupName);
            Assert.Equal(i, match.Data!.LabelStartPageNumber);
            Assert.Equal($"value-{i}", Assert.Single(match.Data.Text!).Text);
        }
    }
}
