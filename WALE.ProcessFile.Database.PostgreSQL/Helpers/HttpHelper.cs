using RateLimiter;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class HttpHelper
{
    private static readonly Lock ConfigurationLock = new();

    private static int _maxRequestsPerSecond = 50;
    private static TimeLimiter? _rateLimiter;

    // Built on first use, never in a field initialiser: a static initialiser captures the rate
    // at type-init time, before the setter can run, and silently discards every configured value.
    public static int MaxRequestsPerSecond
    {
        get => _maxRequestsPerSecond;

        set
        {
            if (value < 1)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "Requests per second must be at least 1");
            }

            lock (ConfigurationLock)
            {
                _maxRequestsPerSecond = value;
                _rateLimiter = null;
            }
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
