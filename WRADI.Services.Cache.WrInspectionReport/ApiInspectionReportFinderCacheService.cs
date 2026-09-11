using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WRADI.Services.Cache.WrInspectionReport.Interfaces;
using WRADI.Services.Cache.WrInspectionReport.Models;

namespace WRADI.Services.Cache.WrInspectionReport;

public class ApiInspectionReportFinderCacheService(HttpClient httpClient) : IInspectionReportFinderCacheService
{
    public async Task<List<InspectionReportFinderResult>> GetInspectionReportFinderResultsAsync(int skip, int take)
    {
        var path = $"/Extractor/InspectionReportFinder/GetResults?skip={skip}&take={take}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<InspectionReportFinderResult>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveInspectionReportFinderResultsAsync(List<InspectionReportFinderResult> results)
    {
        var path = "/Extractor/InspectionReportFinder/SaveResults";
        var json = JsonSerializer.Serialize(results, JsonHelper.GetSerializerOptions());

        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearInspectionReportFinderResultsAsync()
    {
        var path = "/Extractor/InspectionReportFinder/ClearResults";

        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }
}
