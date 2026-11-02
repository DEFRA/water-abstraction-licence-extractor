using System.Collections.Concurrent;

namespace WALE.ProcessFile.Core.Helpers;

/// Caches the Task, not its result, so concurrent callers missing a cold entry await one fetch.
/// Every caller gets the same instance back, so none of them may mutate it.
public static class ReferenceDataCache
{
    /// The data changes only on a NALD reimport, but a warm Lambda container outlives a run.
    public static readonly TimeSpan DefaultTimeToLive = TimeSpan.FromMinutes(30);

    private static readonly ConcurrentDictionary<string, (DateTime FetchedUtc, object Fetch)> Entries = new();

    /// Held only while swapping the entry - no fetch runs inside it. ConcurrentDictionary's own
    /// factories can run more than once under contention, which would defeat the point.
    private static readonly Lock Gate = new();

    public static Task<T> GetAsync<T>(string key, TimeSpan timeToLive, Func<Task<T>> fetch)
    {
        Lazy<Task<T>> entry;

        lock (Gate)
        {
            if (!Entries.TryGetValue(key, out var existing)
                || DateTime.UtcNow - existing.FetchedUtc >= timeToLive)
            {
                existing = (DateTime.UtcNow,
                    new Lazy<Task<T>>(fetch, LazyThreadSafetyMode.ExecutionAndPublication));

                Entries[key] = existing;
            }

            entry = (Lazy<Task<T>>)existing.Fetch;
        }

        // Started outside the lock: Lazy already guarantees one caller runs the fetch.
        return AwaitAsync(key, entry);
    }

    /// Evicts before the exception reaches the caller, so a caller that retries immediately after
    /// a failure cannot be handed the same faulted entry.
    private static async Task<T> AwaitAsync<T>(string key, Lazy<Task<T>> entry)
    {
        try
        {
            return await entry.Value;
        }
        catch
        {
            lock (Gate)
            {
                // Identity-checked so an older fetch failing cannot evict a newer entry.
                if (Entries.TryGetValue(key, out var current) && ReferenceEquals(current.Fetch, entry))
                {
                    Entries.TryRemove(key, out _);
                }
            }

            throw;
        }
    }
}
