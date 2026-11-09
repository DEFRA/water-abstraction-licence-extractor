using System.Diagnostics;
using Xunit;
using DatabaseHttpHelper = WALE.ProcessFile.Database.PostgreSQL.Helpers.HttpHelper;
using ServicesHttpHelper = WALE.ProcessFile.Services.Helpers.HttpHelper;

namespace WALE.ProcessFile.Services.Tests.UnitTests;

// MaxRequestsPerSecond and its limiter are process-wide statics, so parallel runs of these would
// configure each other.
[Collection(nameof(HttpHelperRateLimitTests))]
[CollectionDefinition(nameof(HttpHelperRateLimitTests), DisableParallelization = true)]
public class HttpHelperRateLimitTests
{
    [Fact]
    public async Task AConfiguredRateIsAppliedRatherThanTheDefault()
    {
        DatabaseHttpHelper.SetMaxRequestsPerSecond(2);

        var stopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 4; i++)
        {
            await DatabaseHttpHelper.RateLimiter.Enqueue(() => Task.CompletedTask);
        }

        stopwatch.Stop();

        // Four calls at 2/sec cannot clear inside the first second; at 50/sec they finish at once.
        Assert.True(
            stopwatch.ElapsedMilliseconds > 900,
            $"4 calls at 2/sec took {stopwatch.ElapsedMilliseconds}ms, so the rate was not applied");
    }

    [Fact]
    public void ChangingTheRateRebuildsTheLimiter()
    {
        DatabaseHttpHelper.SetMaxRequestsPerSecond(7);
        var first = DatabaseHttpHelper.RateLimiter;

        DatabaseHttpHelper.SetMaxRequestsPerSecond(11);

        Assert.NotSame(first, DatabaseHttpHelper.RateLimiter);
        Assert.Equal(11, DatabaseHttpHelper.MaxRequestsPerSecond);
    }

    [Fact]
    public void TheSameLimiterIsReusedWhileTheRateIsUnchanged()
    {
        DatabaseHttpHelper.SetMaxRequestsPerSecond(5);

        Assert.Same(DatabaseHttpHelper.RateLimiter, DatabaseHttpHelper.RateLimiter);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ARateBelowOneIsRefused(int rate)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => DatabaseHttpHelper.SetMaxRequestsPerSecond(rate));
    }

    [Theory]
    [InlineData(50, 10, 5)]
    [InlineData(50, 1, 50)]
    [InlineData(50, 20, 2)]
    [InlineData(50, 0, 50)]
    [InlineData(5, 10, 1)]
    public void TheIngressBudgetIsSplitEvenlyAcrossInstances(int total, int instances, int expected)
    {
        Assert.Equal(expected, ServicesHttpHelper.PerInstanceRequestsPerSecond(total, instances));
    }

    [Fact]
    public void BuildingTheClientAppliesTheRateToTheSharedLimiter()
    {
        DatabaseHttpHelper.SetMaxRequestsPerSecond(50);

        using var client = ServicesHttpHelper.GetResilientHttpClient("https://example.invalid", 100, 5);

        Assert.Equal(5, DatabaseHttpHelper.MaxRequestsPerSecond);
    }
}
