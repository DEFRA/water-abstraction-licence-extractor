using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Services;

public class ApiMessageQueueService(HttpClient httpClient) : IMessageQueueService
{
    public async Task AddToFileProcessQueue(SingleFileProcessRequest request)
    {
        var path = "/BFF/Message/SendFileProcessSingleMessage";
        var json = JsonSerializer.Serialize(request, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        
        response.EnsureSuccessStatusCode();
    }
}