using System;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using WRADI.Services.LicenceFinder.Models;

namespace WRADI.Services.LicenceFinder.Implementations;

public class NaldApiClient
{
    public NaldApiClient(string apiBaseUrl)
    {
        var clientHandler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
        };
        
        HttpClient = new HttpClient(clientHandler);
        HttpClient.BaseAddress = new Uri(apiBaseUrl);
    }
    
    private HttpClient HttpClient { get; set; }

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
        
        var response = await HttpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldDataCollection>(
            content,
            GetSerializerOptions())!;
    }
    
    public async Task<NaldLicenceStatusData> GetNaldLicenceStatusDataAsync(short? regionCode = null)
    {
        var path = "/Extractor/NaldData/GetLicenceStatusData";
        
        if (regionCode != null)
        {
            path += $"?regionCode={regionCode}";            
        }
        
        var response = await HttpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var content = await response.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<NaldLicenceStatusData>(
            content,
            GetSerializerOptions())!;
    }
    
    public async Task<string?> GetImportRunDateAsync(string dataSource)
    {
        var path = $"/Extractor/Import/GetDate?dataSource={dataSource}";

        var response = await HttpClient.GetAsync(path);
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadAsStringAsync();
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