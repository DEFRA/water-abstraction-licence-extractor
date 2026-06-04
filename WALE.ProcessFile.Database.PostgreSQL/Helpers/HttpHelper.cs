using RateLimiter;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class HttpHelper
{
    private const int MaxRequestsPerSecond = 10;
    
    public static readonly TimeLimiter RateLimiter =
        TimeLimiter.GetFromMaxCountByInterval(MaxRequestsPerSecond, TimeSpan.FromSeconds(1));
}