using RateLimiter;

namespace WALE.ProcessFile.Database.PostgreSQL.Helpers;

public static class HttpHelper
{
    public static void SetMaxRequestsPerSecond(int maxRequestsPerSecond)
    {
        _maxRequestsPerSecond = maxRequestsPerSecond;
    }

    private static int _maxRequestsPerSecond = 50;
    
    public static TimeLimiter RateLimiter =>
        TimeLimiter.GetFromMaxCountByInterval(_maxRequestsPerSecond, TimeSpan.FromSeconds(1));
}