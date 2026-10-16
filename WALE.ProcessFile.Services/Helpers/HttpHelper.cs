using System.Net;
using Microsoft.Extensions.Http;
using Polly;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Services.Helpers;

public static class HttpHelper
{
    public static IAsyncPolicy<HttpResponseMessage> GetTooManyRequestsBackoffPolicy()
    {
        var jitter = new Random();

        return Policy<HttpResponseMessage>
            .HandleResult(res => res.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(
                5,
                sleepDurationProvider: attempt =>
                    TimeSpan.FromMilliseconds(Math.Pow(2, attempt) * 250 + jitter.Next(0, 250)),
                onRetry: (_, delay, attempt, _) => ConsoleHelper.WriteLine(
                    $"WARNING - HttpHelper - 429 received from API, retry {attempt} in {delay.TotalMilliseconds:F0}ms"));
    }

    public static HttpClient GetResilientHttpClient(string baseUrl, int defaultConnectionLimit, int maxRequestsPerSecond)
    {
        Database.PostgreSQL.Helpers.HttpHelper.MaxRequestsPerSecond = maxRequestsPerSecond;
        
        #pragma warning disable SYSLIB0014
        ServicePointManager.DefaultConnectionLimit = defaultConnectionLimit;
        #pragma warning restore SYSLIB0014
    
        var pollyHandler = new PolicyHttpMessageHandler(GetTooManyRequestsBackoffPolicy())
        {
            InnerHandler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            }
        };
    
        var httpClient = new HttpClient(pollyHandler);
        httpClient.BaseAddress = new Uri(baseUrl);
        
        return httpClient;
    }
}