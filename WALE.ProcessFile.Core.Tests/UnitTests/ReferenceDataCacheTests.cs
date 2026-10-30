using WALE.ProcessFile.Core.Helpers;
using Xunit;

namespace WALE.ProcessFile.Core.Tests.UnitTests;

public class ReferenceDataCacheTests
{
    private static string NewKey() => Guid.NewGuid().ToString();

    [Fact]
    public async Task ConcurrentCallersOnAColdEntryShareOneFetch()
    {
        var key = NewKey();
        var fetches = 0;
        var release = new TaskCompletionSource();

        async Task<int> Fetch()
        {
            Interlocked.Increment(ref fetches);
            await release.Task;
            return 7;
        }

        var callers = Enumerable.Range(0, 50)
            .Select(_ => ReferenceDataCache.GetAsync(key, TimeSpan.FromMinutes(5), Fetch))
            .ToList();

        release.SetResult();
        var results = await Task.WhenAll(callers);

        Assert.Equal(1, fetches);
        Assert.All(results, r => Assert.Equal(7, r));
    }

    [Fact]
    public async Task AnEntryWithinItsWindowIsNotFetchedAgain()
    {
        var key = NewKey();
        var fetches = 0;

        Task<int> Fetch() => Task.FromResult(Interlocked.Increment(ref fetches));

        await ReferenceDataCache.GetAsync(key, TimeSpan.FromMinutes(5), Fetch);
        await ReferenceDataCache.GetAsync(key, TimeSpan.FromMinutes(5), Fetch);

        Assert.Equal(1, fetches);
    }

    [Fact]
    public async Task AnExpiredEntryIsFetchedAgain()
    {
        var key = NewKey();
        var fetches = 0;

        Task<int> Fetch() => Task.FromResult(Interlocked.Increment(ref fetches));

        await ReferenceDataCache.GetAsync(key, TimeSpan.Zero, Fetch);
        await ReferenceDataCache.GetAsync(key, TimeSpan.Zero, Fetch);

        Assert.Equal(2, fetches);
    }

    [Fact]
    public async Task AFailedFetchIsNotCached()
    {
        var key = NewKey();
        var attempts = 0;

        Task<int> Fetch()
        {
            attempts++;
            return attempts == 1
                ? Task.FromException<int>(new InvalidOperationException("boom"))
                : Task.FromResult(42);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => ReferenceDataCache.GetAsync(key, TimeSpan.FromMinutes(5), Fetch));

        Assert.Equal(42, await ReferenceDataCache.GetAsync(key, TimeSpan.FromMinutes(5), Fetch));
    }
}
