using System.Text.Json;
using WALE.ProcessFile.Core.Helpers;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Database.PostgreSQL.Helpers;
using WRADI.Core.AbstractionLicence.Interfaces;
using WRADI.Core.AbstractionLicence.Models;

namespace WRADI.Services.Cache.AbstractionLicence;

public class ApiAbstractionLicenceCacheService(HttpClient httpClient) : IAbstractionLicenceCacheService
{
    public string? CacheFolderOrUrl { get; set; } = httpClient.BaseAddress?.ToString();
    
    public async Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync()
    {
        var path = "/Extractor/LicenceFinder/GetResults";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<LicenceFinderResult>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }
    
    public async Task<List<NaldLinkedLicenceRawData>> GetNaldLinkedLicenceRawDataAsync()
    {
        var path = "/Extractor/LinkedLicence/GetMap";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<NaldLinkedLicenceRawData>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NaldDataCollection> GetNaldDataAsync(
        short? regionCode,
        bool allVersions,
        int skip,
        int take)
    {
        var path = $"/Extractor/NaldData/GetAll?skip={skip}&take={take}";

        if (regionCode != null)
        {
            path += $"&regionCode={regionCode}";
        }
        
        if (allVersions)
        {
            path += "&allVersions=true";
        }

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldDataCollection>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode = null)
    {
        var path = "/Extractor/NaldData/GetLicenceStatusData";
        
        if (regionCode != null)
        {
            path += $"?regionCode={regionCode}";
        }
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldLicenceStatusData>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public Task<(
            HashSet<(string, int)> Live,
            HashSet<(string, int)> Lapsed,
            HashSet<(string, int)> Expired,
            HashSet<(string, int)> Revoked,
            HashSet<(string, int)> Impoundment)>
        GetNaldLicenceNumbersAsync(short? regionCode)
    {
        throw new NotImplementedException();
    }

    public async Task<int> GetNaldLicenceIncrementNumberAsync(string permitNumber, int issueNumber)
    {
        var path = $"/Extractor/NaldData/GetCurrentIncrementNumber?permitNumber={permitNumber}" +
            $"&issueNumber={issueNumber}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return int.Parse(content);
    }

    public async Task<NaldImpoundmentData?> GetNaldImpoundmentLicenceAsync(string licenceNumber, int regionCode)
    {
        var path = $"/Extractor/NaldData/GetImpoundment?licenceNumber={licenceNumber}&regionCode={regionCode}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }
        
        return JsonSerializer.Deserialize<NaldImpoundmentData>(
            content,
            JsonHelper.GetSerializerOptions());
    }

    public async Task<List<LicenceFinderResult>> GetLicenceFinderResultsAsync(int skip, int take)
    {
        var dtStart = DateTime.UtcNow;
        ConsoleHelper.WriteLine($"INFO - {nameof(ApiAbstractionLicenceCacheService)} - Started getting licence finder results");
        
        var path = $"/Extractor/LicenceFinder/GetResults?skip={skip}&take={take}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<LicenceFinderResult>>(
            content,
            JsonHelper.GetSerializerOptions())!;
        
        var tsDuration = (DateTime.UtcNow - dtStart).TotalSeconds;
        ConsoleHelper.WriteLine($"INFO - {nameof(ApiAbstractionLicenceCacheService)} - Finished getting {list.Count} licence finder results in {tsDuration} seconds");
        
        return list;
    }

    public async Task SaveLicenceFinderResultsAsync(List<LicenceFinderResult> results)
    {
        var path = "/Extractor/LicenceFinder/SaveResults";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearLicenceFinderResultsAsync()
    {
        var path = "/Extractor/LicenceFinder/ClearResults";
        
        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<VersionFileToDownload>> GetVersionFilesToDownloadAsync()
    {
        var path = "/Extractor/VersionFiles/GetToDownload";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<VersionFileToDownload>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveVersionFilesToDownloadAsync(List<VersionFileToDownload> results)
    {
        var path = "/Extractor/VersionFiles/SaveToDownload";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<VersionFile>> GetVersionFilesAsync()
    {
        var path = "/Extractor/VersionFiles/GetAll";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<VersionFile>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task SaveVersionFilesAsync(List<VersionFile> results)
    {
        var path = "/Extractor/VersionFiles/SaveAll";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, JsonHelper.GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.PostAsync(new Uri(httpClient.BaseAddress!, path), httpContent));
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearVersionFilesAsync()
    {
        var path = "/Extractor/VersionFiles/ClearAllFiles";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.DeleteAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearVersionFilesToDownloadAsync()
    {
        var path = "/Extractor/VersionFiles/ClearDownloadFiles";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.DeleteAsync(new Uri(httpClient.BaseAddress!, path)));
        
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<NaldLicence>> GetNaldImpoundmentAndAbstractionLicencesAsync()
    {
        var dtStart = DateTime.UtcNow;
        ConsoleHelper.WriteLine($"INFO - {nameof(ApiAbstractionLicenceCacheService)} - Started getting abstraction licences");
        
        var path = "/Extractor/NaldData/GetImpoundmentAndAbstractionLicences";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var list = JsonSerializer.Deserialize<List<NaldLicence>>(
            content,
            JsonHelper.GetSerializerOptions())!;
        
        var tsDuration = (DateTime.UtcNow - dtStart).TotalSeconds;
        ConsoleHelper.WriteLine($"INFO - {nameof(ApiAbstractionLicenceCacheService)} - Finished getting {list.Count} abstraction licences in {tsDuration} seconds");
        
        return list;
    }

    public async Task<NaldAbstractionData?> GetNaldAbstractionLicenceAsync(string licenceNumber, int regionCode)
    {
        var path = $"/Extractor/NaldData/Get?licenceNumber={licenceNumber}&regionCode={regionCode}";
        
        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();

        if (string.IsNullOrEmpty(content))
        {
            return null;
        }
        
        return JsonSerializer.Deserialize<NaldAbstractionData>(
            content,
            JsonHelper.GetSerializerOptions());
    }

    public async Task<LicenceFinderResult> GetLicenceFinderResultAsync(Guid fileId)
    {
        var path = $"/Extractor/LicenceFinder/Get?fileId={fileId}";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));

        var content = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        
        return JsonSerializer.Deserialize<LicenceFinderResult>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }

    public async Task<Dictionary<string, NaldLicenceNumberHistory>> GetNaldLicenceNumberHistoryAsync()
    {
        var path = "/Extractor/NaldData/GetNaldLicenceNumberHistory";

        var response = await HttpHelper.RateLimiter.Enqueue(() =>
            httpClient.GetAsync(path));
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<Dictionary<string, NaldLicenceNumberHistory>>(
            content,
            JsonHelper.GetSerializerOptions())!;
    }
}