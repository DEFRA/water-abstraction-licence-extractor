using System.Net;
using Microsoft.Extensions.Http;
using Polly;

namespace WALE.ProcessFile.Services.Helpers;

public static class HttpHelper
{
    public static HttpClient GetResilientHttpClient(string baseUrl)
    {
        #pragma warning disable SYSLIB0014
        ServicePointManager.DefaultConnectionLimit = 100;
        #pragma warning restore SYSLIB0014
    
        var backoffPolicy = Policy<HttpResponseMessage>
            .HandleResult(res => res.StatusCode == HttpStatusCode.TooManyRequests)
            .WaitAndRetryAsync(2,
                sleepDurationProvider: (_, _) => TimeSpan.FromMilliseconds(1000),
                onRetry: (_, _, _) => { });

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