using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;

namespace WALE.ProcessFile.Services.Services;

public class OrchestratorService(HttpClient httpClient) : IOrchestratorService
{
    public async Task AddToFileProcessQueue(SingleFileProcessRequest request)
    {
        var path = $"/BFF/Message/AddFileToProcess?filePath={request.FilePath}&processRunId={request.ProcessRunId}";
        
        var response = await httpClient.GetAsync(new Uri(httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();
    }
}