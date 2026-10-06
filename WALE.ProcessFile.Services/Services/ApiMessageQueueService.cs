using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;

namespace WALE.ProcessFile.Services.Services;

public class ApiMessageQueueService(HttpClient httpClient) : IMessageQueueService
{
    public async Task AddToFileProcessQueue(FileProcessSingleRequest request)
    {
        var path = "/BFF/Message/SendFileProcessSingleMessage";
        var json = JsonSerializer.Serialize(request, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        
        response.EnsureSuccessStatusCode();
    }

    public async Task<FileProcessQueueBatchResult> AddToFileProcessQueueBatch(
        IReadOnlyList<FileProcessSingleRequest> fileProcessSingleRequests)
    {
        if (fileProcessSingleRequests.Count == 0)
        {
            return new FileProcessQueueBatchResult();
        }

        var path = "/BFF/Message/SendFileProcessSingleMessages";
        var json = JsonSerializer.Serialize(fileProcessSingleRequests, JsonHelper.GetSerializerOptions());

        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));

        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadAsStringAsync();

        // A body that won't deserialise would otherwise read as "nothing failed", which is the one
        // wrong answer to give here - treat it as the whole batch being unaccounted for instead.
        return JsonSerializer.Deserialize<FileProcessQueueBatchResult>(body, JsonHelper.GetSerializerOptions())
            ?? new FileProcessQueueBatchResult
            {
                Requested = fileProcessSingleRequests.Count,
                Enqueued = 0,
                FailedFileIds = fileProcessSingleRequests.Select(r => r.FileId).ToList()
            };
    }
}