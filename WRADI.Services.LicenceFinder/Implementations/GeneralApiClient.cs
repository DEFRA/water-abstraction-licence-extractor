using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WALE.ProcessFile.Core.Interfaces;
using WALE.ProcessFile.Core.Models;
using WALE.ProcessFile.Core.Models.Dms;

namespace WRADI.Services.LicenceFinder.Implementations;

public class GeneralApiClient(IHttpClientFactory httpClientFactory) : IGeneralApiClient
{
    private readonly HttpClient _httpClient = httpClientFactory.CreateClient("GeneralApi");
    
    public async Task<List<DmsFileIdInformation>> GetDmsFileIdInformationAsync()
    {
        var path = "/Extractor/Dms/GetFileIds";

        var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<DmsFileIdInformation>>(
            content,
            GetSerializerOptions())!;
    }
    
    public async Task AddDmsFileIdInformationAsync(DmsFileIdInformation newDmsFileIdInformation)
    {
        var path = "/Extractor/Dms/AddFileIdInformation";
        var json = JsonSerializer.Serialize(newDmsFileIdInformation, GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(new Uri(_httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task<(List<DmsExtract> Data, string ImportDate)> GetDmsExtractAsync(
        int skip,
        int take,
        string extractType)
    {
        var path = $"/Extractor/Dms/GetExtract?skip={skip}&take={take}&extractType={extractType}";

        var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<List<DmsExtract>>(
            content,
            GetSerializerOptions())!;

        return (data, await GetImportRunDateAsync("DmsExtract") ?? "Unknown");
    }
    
    private async Task<string?> GetImportRunDateAsync(string dataSource)
    {
        var path = $"/Extractor/Import/GetDate?dataSource={dataSource}";

        var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
    }
    
    public async Task<List<DmsFileReaderResult>> GetDmsFileReaderResultsAsync()
    {
        var path = "/Extractor/Dms/GetDmsFileReaderResults";

        var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<DmsFileReaderResult>>(
            content,
            GetSerializerOptions())!;
    }
    
    public async Task SaveLicenceFinderResultsAsync(List<LicenceMatchResult> results)
    {
        var path = "/Extractor/LicenceFinder/SaveResults";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(new Uri(_httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task ClearLicenceFinderResultsAsync()
    {
        var path = "/Extractor/LicenceFinder/ClearResults";

        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(new Uri(_httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveInspectionReportFinderResultsAsync(List<DmsExtract> results)
    {
        var path = "/Extractor/InspectionReportFinder/SaveResults";
        var json = JsonSerializer.Serialize(results, GetSerializerOptions());

        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(new Uri(_httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearInspectionReportFinderResultsAsync()
    {
        var path = "/Extractor/InspectionReportFinder/ClearResults";

        var httpContent = new StringContent("{}", System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(new Uri(_httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveVersionFilesToDownloadAsync(List<DownloadInfoMissing> results)
    {
        var path = "/Extractor/VersionFiles/SaveToDownload";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(new Uri(_httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }

    public async Task SaveVersionFilesAsync(List<DownloadInfoAll> results)
    {
        var path = "/Extractor/VersionFiles/SaveAll";
        var json = JsonSerializer.Serialize(new
        {
            results
        }, GetSerializerOptions());
        
        var httpContent = new StringContent(json, System.Text.Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(new Uri(_httpClient.BaseAddress!, path), httpContent);
        response.EnsureSuccessStatusCode();
    }
    
    public async Task ClearVersionFilesAsync()
    {
        var path = "/Extractor/VersionFiles/ClearAllFiles";
        
        var response = await _httpClient.DeleteAsync(new Uri(_httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();
    }

    public async Task ClearVersionFilesToDownloadAsync()
    {
        var path = "/Extractor/VersionFiles/ClearDownloadFiles";
        
        var response = await _httpClient.DeleteAsync(new Uri(_httpClient.BaseAddress!, path));
        response.EnsureSuccessStatusCode();
    }

    public async Task<List<LicenceMatchResult>> GetLicenceFinderResultsAsync(int skip, int take)
    {
        var path = $"/Extractor/LicenceFinder/GetResults?skip={skip}&take={take}";

        var response = await _httpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<List<LicenceMatchResult>>(
            content,
            GetSerializerOptions())!;
    }

    // TODO - In time this should come from the other project as a NuGet reference
    private static JsonSerializerOptions GetSerializerOptions()
    {
        _options ??= new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        return _options;
    }
    
    private static JsonSerializerOptions? _options;
}