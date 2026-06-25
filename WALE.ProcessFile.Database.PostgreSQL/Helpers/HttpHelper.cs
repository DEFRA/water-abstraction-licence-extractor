using RateLimiter;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class HttpHelper
{
    public static int MaxRequestsPerSecond = 50;
    
    public static readonly TimeLimiter RateLimiter =
        TimeLimiter.GetFromMaxCountByInterval(MaxRequestsPerSecond, TimeSpan.FromSeconds(1));
}