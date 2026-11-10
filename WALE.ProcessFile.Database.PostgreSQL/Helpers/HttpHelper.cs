using RateLimiter;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class HttpHelper
{
    private static readonly Lock ConfigurationLock = new();

    private static int _maxRequestsPerSecond = 50;
    private static TimeLimiter? _rateLimiter;

    public static int MaxRequestsPerSecond => _maxRequestsPerSecond;

    public static void SetMaxRequestsPerSecond(int maxRequestsPerSecond)
    {
        if (maxRequestsPerSecond < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRequestsPerSecond),
                maxRequestsPerSecond,
                "Requests per second must be at least 1");
        }

        lock (ConfigurationLock)
        {
            _maxRequestsPerSecond = maxRequestsPerSecond;
            _rateLimiter = null;
        }
    }

    public static TimeLimiter RateLimiter
    {
        get
        {
            var limiter = _rateLimiter;

            if (limiter != null)
            {
                return limiter;
            }

            lock (ConfigurationLock)
            {
                return _rateLimiter ??= TimeLimiter.GetFromMaxCountByInterval(
                    _maxRequestsPerSecond,
                    TimeSpan.FromSeconds(1));
            }
        }
    }
}