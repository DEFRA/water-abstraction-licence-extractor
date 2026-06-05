using System.Net;
using Microsoft.Extensions.Http;
using Polly;
using WALE.ProcessFile.Core.Helpers;

namespace WALE.ProcessFile.Services.Helpers;

public static class HttpHelper
{
    public static HttpClient GetResilientHttpClient(string baseUrl, int defaultConnectionLimit, int maxRequestsPerSecond)
    {
        Database.PostgreSQL.Helpers.HttpHelper.MaxRequestsPerSecond = maxRequestsPerSecond;
        
        #pragma warning disable SYSLIB0014
        ServicePointManager.DefaultConnectionLimit = defaultConnectionLimit;
        #pragma warning restore SYSLIB0014
    
        var backoffPolicy = Policy<HttpResponseMessage>
            .HandleResult(res => res.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(2,
                sleepDurationProvider: (_, _) => TimeSpan.FromMilliseconds(1000),
                onRetry: (_, _, _) => { ConsoleHelper.WriteLine("WARNING - HttpHelper - 429 received from API, retrying"); });

        var pollyHandler = new PolicyHttpMessageHandler(backoffPolicy)
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